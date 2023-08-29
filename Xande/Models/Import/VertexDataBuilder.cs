using Dalamud.Logging;
using Lumina.Data.Parsing;
using Lumina.Models.Models;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Xande.Models.Import {
    internal class VertexDataBuilder {
        public Dictionary<int, int>? BlendIndicesDict = null;
        private Dictionary<string, IReadOnlyDictionary<string, Accessor>> ShapesAccessor = new();
        public List<Vector4>? Bitangents = null;
        public List<(List<Vector3> pos, float weight)> AppliedShapePositions = new();
        public List<(List<Vector3> nor, float weight)> AppliedShapeNormals = new();
        public bool ApplyShapes = true;

        private List<Vector3>? _positions = null;
        private List<Vector4>? _blendWeights = null;
        private List<Vector4>? _blendIndices = null;
        private List<Vector3>? _normals = null;
        private List<Vector2>? _texCoords = null;
        private List<Vector4>? _tangent1 = null;
        private List<Vector4>? _colors = null;

        private MdlStructs.VertexDeclarationStruct _vertexDeclaration;

        public VertexDataBuilder( MeshPrimitive primitive, MdlStructs.VertexDeclarationStruct vertexDeclaration ) {
            _vertexDeclaration = vertexDeclaration;
            _positions = primitive.GetVertexAccessor( "POSITION" )?.AsVector3Array().ToList();
            _blendWeights = primitive.GetVertexAccessor( "WEIGHTS_0" )?.AsVector4Array().ToList();
            _blendIndices = primitive.GetVertexAccessor( "JOINTS_0" )?.AsVector4Array().ToList();
            _normals = primitive.GetVertexAccessor( "NORMAL" )?.AsVector3Array().ToList();
            _texCoords = primitive.GetVertexAccessor( "TEXCOORD_0" )?.AsVector2Array().ToList();
            _tangent1 = primitive.GetVertexAccessor( "TANGENT" )?.AsVector4Array().ToList();
            _colors = primitive.GetVertexAccessor( "COLOR_0" )?.AsVector4Array().ToList();
        }

        public void AddShape( string shapeName, IReadOnlyDictionary<string, Accessor> accessor ) {
            ShapesAccessor.Add( shapeName, accessor );
        }

        public List<byte> GetVertexData( int index, MdlStructs.VertexElement ve, string? shapeName = null ) {
            return GetBytes( GetVector4( index, ( Vertex.VertexUsage )ve.Usage, shapeName ), ( Vertex.VertexType )ve.Type, ( Vertex.VertexUsage )ve.Usage );
        }

        public Dictionary<int, List<byte>> GetVertexData() {
            var streams = new Dictionary<int, List<byte>>();
            for( var vertexId = 0; vertexId < _positions?.Count; vertexId++ ) {
                foreach( var ve in _vertexDeclaration.VertexElements ) {
                    if( ve.Stream == 255 ) break;
                    if( !streams.ContainsKey( ve.Stream ) ) {
                        streams.Add( ve.Stream, new List<byte>() );
                    }

                    streams[ve.Stream].AddRange( GetVertexData( vertexId, ve ) );
                }
            }

            return streams;
        }

        public Dictionary<int, List<byte>> GetShapeVertexData( List<int> diffVertices, string? shapeName = null ) {
            var streams = new Dictionary<int, List<byte>>();
            if( ShapesAccessor == null ) {
                PluginLog.Error( $"Shape accessor was null" );
            }

            foreach( var vertexId in diffVertices ) {
                foreach( var ve in _vertexDeclaration.VertexElements ) {
                    if( ve.Stream == 255 ) { break; }
                    if( !streams.ContainsKey( ve.Stream ) ) {
                        streams.Add( ve.Stream, new List<byte>() );
                    }

                    streams[ve.Stream].AddRange( GetVertexData( vertexId, ve, shapeName ) );
                }
            }

            return streams;
        }

        private List<Vector3> GetShapePositions( string shapeName ) {
            //ShapesAccessor.TryGetValue( shapeName, out var dict );
            /*
            var dict = ShapesAccessor[shapeName];
            Accessor? accessor = null;
            dict?.TryGetValue( "POSITION", out accessor );
            return accessor?.AsVector3Array().ToList();
            */
            return ShapesAccessor[shapeName]["POSITION"].AsVector3Array().ToList();
        }

        private List<Vector3> GetShapeNormals( string shapeName ) {
            //ShapesAccessor.TryGetValue( shapeName, out var dict );
            /*
            var dict = ShapesAccessor[shapeName];
            Accessor? accessor = null;
            dict?.TryGetValue( "NORMAL", out accessor );
            return accessor?.AsVector3Array().ToList();
            */
            return ShapesAccessor[shapeName]["NORMAL"].AsVector3Array().ToList();
        }

        private Vector4 GetVector4( int index, Vertex.VertexUsage usage, string? shapeName = null ) {
            var vector4 = new Vector4( 0, 0, 0, 0 );
            switch( usage ) {
                case Vertex.VertexUsage.Position:
                    vector4 = new Vector4( _positions[index], 0 );
                    if( shapeName != null ) {
                        var shapePositions = GetShapePositions( shapeName );
                        vector4 += new Vector4( shapePositions[index], 0 );
                    }
                    if( ApplyShapes ) {
                        foreach( var appliedShape in AppliedShapePositions ) {
                            var list = appliedShape.pos;
                            var weight = appliedShape.weight;

                            if( list.Count > index ) {
                                vector4 += new Vector4( list[index] * weight, 0 );
                            }
                        }
                    }
                    break;
                case Vertex.VertexUsage.BlendWeights:
                    vector4 = _blendWeights?[index] ?? vector4;
                    break;
                case Vertex.VertexUsage.BlendIndices:
                    if( _blendIndices == null ) break;
                    vector4 = _blendIndices[index];
                    if( BlendIndicesDict != null ) {
                        for( var i = 0; i < 4; i++ ) {
                            if( BlendIndicesDict.ContainsKey( ( int )_blendIndices[index][i] ) ) {
                                vector4[i] = BlendIndicesDict[( int )_blendIndices[index][i]];
                            }
                        }
                    }
                    break;
                case Vertex.VertexUsage.Normal:
                    if( _normals != null ) {
                        vector4 = new Vector4( _normals[index], 0 );
                        if (shapeName != null) {
                            var shapeNormals = GetShapeNormals( shapeName );
                            vector4 += new Vector4( shapeNormals[index], 0 );
                        }
                        if( ApplyShapes ) {
                            foreach( var appliedShape in AppliedShapeNormals ) {
                                var list = appliedShape.nor;
                                var weight = appliedShape.weight;

                                if( list.Count > index ) {
                                    vector4 += new Vector4( list[index] * weight, 0 );
                                }
                            }
                        }
                    }
                    else {
                        PluginLog.Error( $"normals were null" );
                        vector4 = new( 1, 1, 1, 1 );
                    }
                    break;
                case Vertex.VertexUsage.UV:
                    if( _texCoords != null ) {
                        vector4 = new( _texCoords[index], 0, 0 );
                    }
                    else {
                        PluginLog.Error( $"tex coordinates were null" );
                    }
                    break;
                case Vertex.VertexUsage.Tangent2:
                    break;
                case Vertex.VertexUsage.Tangent1:
                    //vector4 = _tangent1?[index] ?? vector4;
                    if( Bitangents != null && Bitangents.Count > index ) {

                        // I don't know why this math sorta works
                        // The values seem "close enough" (where I think the difference is due to floating point arithmetic)
                        var vec = new Vector3( Bitangents[index].X, Bitangents[index].Y, Bitangents[index].Z );
                        vec = Vector3.Normalize( vec );
                        //vector4 = Bitangents?[index] + new Vector4(1, 1, 1, 0) ?? vector4;    // maybe??
                        //vector4 = Bitangents[index];
                        var val = ( Vector3.One - vec ) / 2;

                        vector4 = new Vector4( val, Bitangents[index].W > 0 ? 0 : 1 );

                    }
                    else {
                        vector4 = new Vector4( 1, 1, 1, 1 );
                    }
                    break;
                case Vertex.VertexUsage.Color:
                    if( _colors != null ) {
                        vector4 = _colors[index];
                    }
                    else {
                        vector4 = new( 1, 1, 1, 1 );
                    }
                    break;
            }
            return vector4;
        }

        private static List<byte> GetBytes( Vector4 vector4, Vertex.VertexType type, Vertex.VertexUsage usage ) {
            var ret = new List<byte>();
            switch( type ) {
                case Vertex.VertexType.Single3:
                    ret.AddRange( BitConverter.GetBytes( vector4.X ) );
                    ret.AddRange( BitConverter.GetBytes( vector4.Y ) );
                    ret.AddRange( BitConverter.GetBytes( vector4.Z ) );
                    break;
                case Vertex.VertexType.Single4:
                    ret.AddRange( BitConverter.GetBytes( vector4.X ) );
                    ret.AddRange( BitConverter.GetBytes( vector4.Y ) );
                    ret.AddRange( BitConverter.GetBytes( vector4.Z ) );
                    ret.AddRange( BitConverter.GetBytes( vector4.W ) );
                    break;
                case Vertex.VertexType.UInt:
                    ret.Add( ( byte )vector4.X );
                    ret.Add( ( byte )vector4.Y );
                    ret.Add( ( byte )vector4.Z );
                    ret.Add( ( byte )vector4.W );
                    break;
                case Vertex.VertexType.ByteFloat4:
                    ret.Add( ( byte )Math.Round( vector4.X * 255f ) );
                    ret.Add( ( byte )Math.Round( vector4.Y * 255f ) );
                    ret.Add( ( byte )Math.Round( vector4.Z * 255f ) );
                    ret.Add( ( byte )Math.Round( vector4.W * 255f ) );
                    break;
                case Vertex.VertexType.Half2:
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.X ) );
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.Y ) );
                    break;
                case Vertex.VertexType.Half4:
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.X ) );
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.Y ) );
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.Z ) );
                    ret.AddRange( BitConverter.GetBytes( ( Half )vector4.W ) );
                    break;
            }
            return ret;
        }
    }
}
