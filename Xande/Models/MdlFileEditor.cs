using Lumina;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xande.Models {
    public class MdlFileEditor {

        public IDictionary<int, IDictionary<int, IList<string>>> Attributes;
        public IDictionary<int, string> Materials;
        public Dictionary<int, string> StringOffsetToStringMap;

        MdlFile _file;
        IEnumerable<byte> _vertexData;
        IEnumerable<byte> _indexData;

        private List<string> _bones = new();
        private List<string> _shapes = new();
        private List<string> _extras = new();
        private readonly ILogger? _logger;

        public MdlFileEditor(MdlFile file, ILogger? logger = null) {
            _file = file;
            _logger = logger;

            using var stream = new MemoryStream( _file.Data );
            using var br = new LuminaBinaryReader( stream );

            // TODO: For now assuming first lod
            br.Seek( _file.FileHeader.VertexOffset[0] );
            _vertexData = br.ReadBytes( ( int )_file.FileHeader.VertexBufferSize[0] );

            br.Seek( _file.FileHeader.IndexOffset[0] );
            _indexData = br.ReadBytes( ( int )_file.FileHeader.IndexBufferSize[0] );

            ReadStrings();
            ReadMaterials();
            ReadAttributes();
        }

        public MdlFileEditor(MdlFile file, IEnumerable<byte> vertexData, IEnumerable<byte> indexData, ILogger? logger = null) {
            _file = file;
            _vertexData = vertexData;
            _indexData = indexData;
            _logger = logger;

            ReadStrings();
            ReadMaterials();
            ReadAttributes();
        }

        public void SetAttributes(IDictionary<int, IDictionary<int, IList<string>>> input) {
            Attributes = input;
        }

        public void SetMaterials(IDictionary<int, string> input) {
            Materials = input;
        }

        public (MdlFile file, List<byte> vertexData, List<byte> indexData) Rebuild() {
            var attributes = new List<string>();
            foreach( var submesh in Attributes.Values ) {
                foreach( var submeshAttributes in submesh.Values ) {
                    foreach( var attr in submeshAttributes ) {
                        if( String.IsNullOrWhiteSpace(attr) && !attributes.Contains( attr ) ) {
                            attributes.Add( attr );
                        }
                    }
                }
            }

            var materials = Materials.Values.Distinct().ToList();
            foreach (var (meshIdx, mat) in Materials) {
                _file.Meshes[meshIdx].MaterialIndex = (ushort)materials.IndexOf( mat );
            }

            // TODO: (multiple?) attributes don't seem to be assigned correctly...?
            var submeshIdx = 0;
            foreach (var submesh in Attributes.Values) {
                foreach (var submeshAttributes in submesh.Values) {
                    var mask = 0;
                    for (var i = 0; i < attributes.Count; i++ ) {
                        if( submeshAttributes.Contains( attributes[i]) ) {
                            mask += ( 1 << i );
                        }
                    }
                    _file.Submeshes[submeshIdx].AttributeIndexMask = (uint)mask;
                    submeshIdx++;
                }
            }
            var stringCount = attributes.Count + _bones.Count + materials.Count + _shapes.Count + _extras.Count;

            _logger?.Debug( $"string count: {stringCount}" );
            _file.FileHeader.MaterialCount = (ushort)materials.Count;
            _file.ModelHeader.MaterialCount = (ushort)materials.Count;

            _file.ModelHeader.AttributeCount = (ushort)attributes.Count;

            var newStrings = RebuildStrings();
            _logger?.Debug( $"string diff is {newStrings.Length}-{_file.Strings.Length} = {newStrings.Length - _file.Strings.Length}" );

            var sizeDiff = newStrings.Length - _file.Strings.Length;
            _file.StringCount = (ushort)stringCount;
            _file.Strings = Encoding.UTF8.GetBytes( newStrings );

            sizeDiff += ( _file.AttributeNameOffsets.Length - attributes.Count ) * sizeof( uint );
            _file.AttributeNameOffsets = new uint[attributes.Count];
            for( var i = 0; i < attributes.Count; i++ ) {
                _file.AttributeNameOffsets[i] = (uint) newStrings.IndexOf( attributes[i] );
            }

            // bone offset size should stay the same
            for (var i = 0; i < _bones.Count; i++ ) {
                _file.BoneNameOffsets[i] = ( uint )newStrings.IndexOf( _bones[i] );
            }
            // TODO: Recalculate the entire name offsets (attributes, bones, materials, shapes, and extras)
            sizeDiff += ( _file.MaterialNameOffsets.Length - materials.Count ) * sizeof( uint );
            _file.MaterialNameOffsets = new uint[materials.Count];
            for (var i = 0; i < materials.Count; i++) {
                _file.MaterialNameOffsets[i] = (uint) newStrings.IndexOf( materials[i] );
            }

            _file.FileHeader.RuntimeSize += (uint)sizeDiff;

            // TODO?: For now, only assuming first lod
            // TODO: This can fail because the Padding value may be different
            _file.FileHeader.VertexOffset[0] += ( uint )sizeDiff;
            _file.FileHeader.IndexOffset[0] += ( uint )sizeDiff;

            return (_file, _vertexData.ToList(), _indexData.ToList());
        }

        private void ReadStrings() {
            StringOffsetToStringMap = new();
            using var br = new LuminaBinaryReader( _file.Strings );
            for( var i = 0; i < _file.StringCount; i++ ) {
                long startOffset = br.BaseStream.Position;
                string tmp = br.ReadStringData();
                StringOffsetToStringMap[( int )startOffset] = tmp;
            }

            var boneStart = _file.ModelHeader.AttributeCount;
            var boneEnd = boneStart + _file.ModelHeader.BoneCount; ;
            _bones = StringOffsetToStringMap.Values.Take(new Range(boneStart, boneEnd)).ToList();

            var shapeStart = boneEnd + _file.ModelHeader.MaterialCount;
            var shapeEnd = shapeStart + _file.ModelHeader.ShapeCount;
            _shapes = StringOffsetToStringMap.Values.Take( new Range( shapeStart, shapeEnd ) ).ToList();
            _extras = StringOffsetToStringMap.Values.Take( new Range( shapeEnd, StringOffsetToStringMap.Values.Count - shapeEnd ) ).ToList();

            _logger?.Debug($"bones: {_bones.Count}");
        }

        private void ReadAttributes() {
            Attributes = new Dictionary<int, IDictionary<int, IList<string>>>();

            for( var meshIdx = 0; meshIdx < _file.Meshes.Length; meshIdx++ ) {
                var mesh = _file.Meshes[meshIdx];
                if( !Attributes.ContainsKey( meshIdx ) ) {
                    Attributes.Add( meshIdx, new Dictionary<int, IList<string>>() );
                }
                for (var submeshIdx = mesh.SubMeshIndex; submeshIdx < mesh.SubMeshIndex + mesh.SubMeshCount; submeshIdx++ ) {
                    var submesh = _file.Submeshes[submeshIdx];

                    var attributesList = new List<string>();
                    for (var i = 0; i < _file.ModelHeader.AttributeCount; i++ ) {
                        if ((1 << i & submesh.AttributeIndexMask) > 0) {
                            var nameOffset = _file.AttributeNameOffsets[i];
                            attributesList.Add( StringOffsetToStringMap[( int )nameOffset] );
                        }
                    }

                    Attributes[meshIdx].Add( submeshIdx, attributesList );
                }
            }
        }

        private void ReadMaterials() {
            Materials = new SortedDictionary<int, string>();
            var mats = new List<string>();
            for (var matIdx = 0; matIdx < _file.FileHeader.MaterialCount; matIdx++ ) {
                var pathOffset = _file.MaterialNameOffsets[matIdx];
                var path = StringOffsetToStringMap[(int)pathOffset];
                mats.Add( path );
            }

            for (var meshIdx = 0; meshIdx < _file.Meshes.Length; meshIdx++) {
                var matIdx = _file.Meshes[meshIdx].MaterialIndex;
                Materials.Add( meshIdx, mats[matIdx] );
            }
        }

        private string RebuildStrings() {
            var attributes = new List<string>();
            foreach (var submesh in Attributes.Values) {
                foreach (var attr in submesh.Values) {
                    foreach (var a in attr) {
                        if (a != "" && !attributes.Contains(a)) {
                            attributes.Add( a );
                        }
                    }
                }
            }
            var materials = Materials.Values.Distinct();
            _logger?.Debug( $"{string.Join( ",", attributes )}" );
            _logger?.Debug( $"{string.Join( ",", _bones )}" );
            _logger?.Debug( $"{string.Join( ",", materials )}" );
            _logger?.Debug( $"{string.Join( ",", _shapes )}" );
            _logger?.Debug( $"{string.Join( ",", _extras )}" );
            var list = new List<string>();
            list.AddRange( attributes );
            list.AddRange( _bones );
            list.AddRange( materials );
            list.AddRange( _shapes );
            list.AddRange( _extras );

            var str = String.Join( "\0", list );

            _logger?.Debug( $"{str}" );
            // I don't know if this is actually necessary
            if( attributes.Count == 0 ) {
                str += "\0";
            }
            if( _bones.Count == 0 ) {
                str += "\0";
            }
            if( materials.Count() == 0 ) {
                str += "\0";
            }
            if( _shapes.Count == 0 ) {
                str += "\0";
            }
            if( _extras.Count == 0 ) {
                str += "\0";
            }

            // This one is required, though
            if( !str.EndsWith( "\0" ) ) {
                str += "\0";
            }
            return str;
        }
    }
}
