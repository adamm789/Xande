using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Lumina;
using Lumina.Data.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xande.Models;

namespace Xande.TestPlugin.Windows {
    internal class MdlFileEditorView {
        private readonly LuminaManager _lumina;
        private readonly FileDialogManager _fileDialogManager;
        private readonly ILogger? _logger;
        private bool _isLoaded = false;
        private MdlFileEditor? _editor = null;

        public string MdlPath = "";
        private string _tempMdlPath = "";
        private string[]? _materials;
        private string[]? _attributes;

        private IList<string>[] _submeshAttributes;

        private string[]? _newAttributes;

        public MdlFileEditorView( LuminaManager lumina, ILogger? logger = null ) {
            _lumina = lumina;
            _logger = logger;
            _fileDialogManager = new FileDialogManager();
        }

        public void Draw() {
            ImGui.InputText( ".mdl file", ref MdlPath, 1024 );
            ImGui.SameLine();
            MdlFileEditor? editor = _editor;
            string[]? materials = null;
            string[]? attributes = null;

            if( ImGui.Button( "Load .mdl" ) || (File.Exists(MdlPath) && _tempMdlPath != MdlPath)) {
                if( !String.IsNullOrWhiteSpace( MdlPath ) ) {
                    var file = _lumina.GetFile<MdlFile>( MdlPath );
                    if( file != null ) {
                        _tempMdlPath = MdlPath;
                        _isLoaded = true;
                        editor = new( file, _logger );
                        _editor = editor;

                        _newAttributes = new string[file.ModelHeader.SubmeshCount];
                        for( var j = 0; j < file.ModelHeader.SubmeshCount; j++ ) {
                            _newAttributes[j] = string.Empty;
                        }
                        _submeshAttributes = new List<string>[file.ModelHeader.SubmeshCount];

                        /*
                        materials = new string[file.ModelHeader.MeshCount];
                        foreach( var (meshIdx, mat) in editor.Materials ) {
                            materials[meshIdx] = mat;
                        }
                        _materials = materials;
                        */

                        materials = new string[file.ModelHeader.MeshCount];
                        foreach( var (meshIdx, mat) in editor.Materials ) {
                            materials[meshIdx] = mat;
                        }
                        _materials = materials;

                        attributes = new string[file.ModelHeader.SubmeshCount];
                        var i = 0;
                        foreach( var (k, v) in editor.Attributes ) {
                            foreach( var (k2, v2) in v ) {
                                attributes[i] = String.Join( ",", v2 );
                                _submeshAttributes[i] = new List<string>( v2 );
                                i++;
                            }
                        }
                        _attributes = attributes;
                    }
                }
            }

            if( _editor != null ) {
                foreach( var (meshIdx, mat) in editor.Materials ) {
                    ImGui.Text( $"Mesh {meshIdx} material" );
                    ImGui.SameLine();
                    ImGui.InputText( $"##material {meshIdx}", ref _materials[meshIdx], 1024 );
                }

                var index = 0;
                foreach( var (meshIdx, submeshes) in editor.Attributes ) {
                    var mesh = editor.Attributes[meshIdx];
                    foreach( var (submeshIdx, attributeList) in mesh ) {
                        ImGui.InputText( $"##{meshIdx}-{submeshIdx}", ref _newAttributes[index], 128 );
                        ImGui.SameLine();
                        if( ImGui.Button( $"Add to {meshIdx}-{submeshIdx}" ) ) {
                            if( _submeshAttributes[index].Contains( _newAttributes[index] ) ) {
                                _logger?.Debug( $"Submesh already contains {_newAttributes[index]}" );
                            }
                            else if( !String.IsNullOrWhiteSpace( _newAttributes[index]) ) {
                                _submeshAttributes[index].Add( _newAttributes[index] );
                                _newAttributes[index] = string.Empty;
                            }
                        }
                        var copy = new List<string>( _submeshAttributes[index] );
                        foreach( var i in copy ) {
                            if( ImGui.Button( $"{index}-{i}" ) ) {
                                _submeshAttributes[index].Remove( i );
                            }
                        }
                        index++;
                    }
                }
            }
        }

        public (MdlFile, IList<byte>, IList<byte>) Confirm() {
            _logger?.Debug( $"Saving..." );
            var materials = new Dictionary<int, string>();
            for( var i = 0; i < _materials.Length; i++ ) {
                _logger?.Debug( _materials[i] );
                materials.Add( i, _materials[i] );
            }
            _editor.SetMaterials( materials );

            var attributes = new Dictionary<int, IDictionary<int, IList<string>>>();
            var submeshIdx = 0;

            foreach( var (k, v) in _editor.Attributes ) {
                attributes.Add( k, new Dictionary<int, IList<string>>() );
                foreach( var (k2, v2) in v ) {
                    _logger?.Debug( $"{k}, {k2}: {String.Join( ", ", _submeshAttributes[submeshIdx] )}" );
                    //var attrList = _attributes[submeshIdx].Split( "," ).ToList();
                    //attributes[k].Add( k2, attrList );
                    attributes[k].Add( k2, _submeshAttributes[submeshIdx] );
                    submeshIdx++;
                }
            }
            _editor.SetAttributes( attributes );

            return _editor.Rebuild();
        }
    }
}
