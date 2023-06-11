using Dalamud.Logging;
using Lumina.Data.Files;
using Lumina.Data.Parsing;
using Lumina.Models.Models;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Mesh = SharpGLTF.Schema2.Mesh;

namespace Xande.Models.Import {
    internal class LuminaMeshBuilder {
        public List<SubmeshBuilder> Submeshes = new();
        public Dictionary<int, List<byte>> VertexData = new();
        public SortedSet<string> Attributes = new();
        public MdlStructs.BoneTableStruct BoneTableStruct;
        public List<string> Bones => _originalBoneIndexToString.Values.ToList();
        public string Material = String.Empty;
        public List<string> Shapes = new();

        private List<string> _skeleton;

        private Dictionary<int, string> _originalBoneIndexToString = new();
        //private MdlStructs.VertexDeclarationStruct _vertexDeclarationStruct;
        private List<Vector4> Bitangents = new();
        private Dictionary<int, int> _blendIndicesDict;

        public int IndexCount { get; protected set; } = 0;
        public int _startIndex = 0;

        public LuminaMeshBuilder(List<SubmeshBuilder> submeshes, int startIndex) {
            //_vertexDeclarationStruct = vds;
            _startIndex = startIndex;

            foreach (var sm in submeshes) {
                Submeshes.Add( sm );
                TryAddBones( sm );
                AddShapes( sm );
                AddAttributes( sm );

                IndexCount += sm.IndexCount;

                if( String.IsNullOrEmpty( Material ) ) {
                    Material = sm.MaterialPath;
                }
                else {
                    if( Material != sm.MaterialPath ) {
                        PluginLog.Error( $"Found multiple materials. Original \"{Material}\" vs \"{sm.MaterialPath}\"" );
                    }
                }
            }

            if (Bones.Count == 0) {
                PluginLog.Warning( $" Mesh had zero bones. This can cause a game crash if a skeleton is expected." );
            }
        }

        /*
        public LuminaMeshBuilder( List<string> skeleton, MdlStructs.VertexDeclarationStruct vds, int startIndex ) {
            _skeleton = skeleton;
            _vertexDeclarationStruct = vds;
            _startIndex = startIndex;
        }

        public void AddSubmesh( Mesh mesh ) {
            var submeshBuilder = new SubmeshBuilder( mesh, _skeleton );
            Submeshes.Add( submeshBuilder );
            TryAddBones( submeshBuilder );
            AddShapes( submeshBuilder );
            AddAttributes( submeshBuilder );

            IndexCount += submeshBuilder.IndexCount;
            if( String.IsNullOrEmpty( Material ) ) {
                Material = submeshBuilder.MaterialPath;
            }
            else {
                if( Material != submeshBuilder.MaterialPath ) {
                    PluginLog.Error( $"Found multiple materials. Original \"{Material}\" vs \"{submeshBuilder.MaterialPath}\"" );
                }
            }

            if (Bones.Count == 0) {
                PluginLog.Warning( $" Mesh {mesh.Name} had zero bones. This can cause a game crash if a skeleton is expected." );
            }
        }
        */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strings">An optional list of strings that may or may not contain the names of used shapes</param>
        /// <returns></returns>
        public int GetVertexCount( bool includeShapes, List<string>? strings = null ) {
            var vertexCount = 0;
            foreach( var submeshBuilder in Submeshes ) {
                vertexCount += submeshBuilder.GetVertexCount( includeShapes, strings );
            }
            return vertexCount;
        }

        public int GetMaterialIndex( List<string> materials ) {
            return materials.IndexOf( Material );
        }

        public MdlStructs.BoneTableStruct GetBoneTableStruct( List<string> bones, List<string> hierarchyBones ) {
            _blendIndicesDict = new();
            var boneTable = new List<ushort>();
            var values = _originalBoneIndexToString.Values.Where( x => x != "n_root" ).ToList();
            var newValues = new List<string>();
            foreach( var b in hierarchyBones ) {
                if( values.Contains( b ) ) {
                    newValues.Add( b );
                }
            }

            foreach( var v in _originalBoneIndexToString ) {
                _blendIndicesDict.Add( v.Key, newValues.IndexOf( v.Value ) );
            }

            foreach( var v in newValues ) {
                var index = bones.IndexOf( v );
                if( index >= 0 ) {
                    boneTable.Add( ( ushort )index );
                }
            }

            var boneCount = boneTable.Count;

            while( boneTable.Count < 64 ) {
                boneTable.Add( 0 );
            }

            foreach (var sm in Submeshes) {
                sm.VertexDataBuilder.BlendIndicesDict = _blendIndicesDict;
            }

            return new() {
                BoneIndex = boneTable.ToArray(),
                BoneCount = ( byte )boneCount
            };
        }

        public List<ushort> GetSubmeshBoneMap( List<string> bones ) {
            var ret = new List<ushort>();
            foreach( var b in _originalBoneIndexToString.Values ) {
                var index = bones.IndexOf( b );
                ret.Add( ( ushort )index );
            }
            return ret;
        }

        public Dictionary<int, List<byte>> GetVertexData() {
            var vertexDict = new Dictionary<int, List<byte>>();
            foreach( var submesh in Submeshes ) {
                var bitangents = submesh.CalculateBitangents();
                Bitangents.AddRange( bitangents);
                //var submeshVertexData = VertexDataBuilder.GetVertexData( submesh, _vertexDeclarationStruct, _blendIndicesDict, bitangents );
                var submeshVertexData = submesh.GetVertexData();

                foreach( var stream in submeshVertexData.Keys ) {
                    if( !vertexDict.ContainsKey( stream ) ) {
                        vertexDict.Add( stream, new() );
                    }
                    vertexDict[stream].AddRange( submeshVertexData[stream] );
                }
            }
            return vertexDict;
        }

        private void TryAddBones( SubmeshBuilder submesh ) {
            foreach( var kvp in submesh.OriginalBoneIndexToStrings ) {
                if( !_originalBoneIndexToString.ContainsKey( kvp.Key ) ) {
                    _originalBoneIndexToString.Add( kvp.Key, kvp.Value );
                }
            }
            if( _originalBoneIndexToString.Keys.Count > 64 ) {
                PluginLog.Error( $"There are currently {_originalBoneIndexToString.Keys.Count} bones, which is over the allowed 64." );
            }
        }

        public Dictionary<string, Dictionary<int, List<byte>>> GetShapeData( List<string>? strings = null ) {
            var ret = new Dictionary<string, Dictionary<int, List<byte>>>();
            foreach( var submesh in Submeshes ) {
                //var submeshShapeData = submesh.SubmeshShapeBuilder.GetVertexData( _vertexDeclarationStruct, strings, _blendIndicesDict );
                //var submeshShapeData = submesh.GetVertexShapeData( _vertexDeclarationStruct, strings, _blendIndicesDict );
                var submeshShapeData = submesh.GetShapeVertexData( strings );
                foreach( var submeshShapeName in submeshShapeData.Keys ) {
                    var submeshShapeVertexData = submeshShapeData[submeshShapeName];
                    if( !ret.ContainsKey( submeshShapeName ) ) {
                        ret.Add( submeshShapeName, submeshShapeVertexData );
                    }
                    else {
                        var shapeDict = ret[submeshShapeName];
                        foreach( var stream in shapeDict.Keys ) {
                            if( submeshShapeVertexData.ContainsKey( stream ) ) {
                                shapeDict[stream].AddRange( submeshShapeVertexData[stream] );
                            }
                        }
                    }
                }
            }
            return ret;
        }

        public List<MdlStructs.ShapeValueStruct> GetShapeValues( string str ) {
            var ret = new List<MdlStructs.ShapeValueStruct>();
            var indexCount = 0;
            foreach( var submesh in Submeshes ) {
                var vals = submesh.GetShapeValues( str );
                foreach (var v in vals) {
                    ret.Add( new() {
                        BaseIndicesIndex = (ushort)(v.BaseIndicesIndex + _startIndex + indexCount),
                        ReplacingVertexIndex = v.ReplacingVertexIndex
                    } );
                }

                indexCount += submesh.IndexCount;
            }
            return ret;
        }

        private void AddShapes( SubmeshBuilder submesh ) {
            /*
            foreach( var s in submesh.SubmeshShapeBuilder.Shapes.Keys ) {
                if( !Shapes.Contains( s ) ) {
                    Shapes.Add( s );
                }
            }
            */
            foreach (var s in submesh.Shapes) {
                if (!Shapes.Contains(s)) {
                    Shapes.Add(s);
                }
            }
        }

        private void AddAttributes( SubmeshBuilder submesh ) {
            foreach( var attr in submesh.Attributes ) {
                if( !Attributes.Contains( attr ) ) {
                    Attributes.Add( attr );
                }
            }
        }
    }
}
