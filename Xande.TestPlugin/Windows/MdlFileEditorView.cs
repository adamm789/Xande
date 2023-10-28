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

        private string _mdlPath = "";
        private string[]? _materials;
        private string[]? _attributes;

        public MdlFileEditorView(LuminaManager lumina, ILogger? logger = null) {
            _lumina = lumina;
            _logger = logger;
            _fileDialogManager = new FileDialogManager();
        }

        public void Draw() {
            ImGui.InputText( ".mdl file", ref _mdlPath, 1024 );
            ImGui.SameLine();
            MdlFileEditor? editor = _editor;
            string[]? materials = null;
            string[]? attributes = null;
            if (ImGui.Button("Load .mdl")) {
                if (!String.IsNullOrWhiteSpace(_mdlPath)) {
                    var file = _lumina.GetFile<MdlFile>(_mdlPath);
                    if (file != null) {
                        _isLoaded = true;
                        editor = new( file, _logger );
                        _editor = editor;

                        materials = new string[file.ModelHeader.MeshCount];
                        foreach (var (meshIdx, mat) in editor.Materials) {
                            materials[meshIdx] = mat;
                        }
                        _materials = materials;

                        attributes = new string[file.ModelHeader.SubmeshCount];
                        var i = 0;
                        foreach (var (k,v) in editor.Attributes) {
                            foreach (var(k2,v2) in v) {
                                attributes[i] = String.Join( ",", v2 );
                                i++;
                            }
                        }
                        _attributes = attributes;
                    }
                }
            }

            if (_editor != null) {
                foreach (var (meshIdx, mat) in editor.Materials) {
                    ImGui.InputText( $"Mesh {meshIdx} material", ref _materials[meshIdx], 1024 );
                }

                var index = 0;
                foreach (var (meshIdx, submeshes) in editor.Attributes) {
                    var mesh = editor.Attributes[meshIdx];
                    foreach( var (submeshIdx, attributeList) in mesh ) {
                        ImGui.InputText( $"{meshIdx}-{submeshIdx}", ref _attributes[index], 1024 );
                        index++;
                    }
                }

            }
        }

        public (MdlFile, IList<byte>, IList<byte>) Confirm() {
            _logger?.Debug( $"Saving..." );
            var materials = new Dictionary<int, string>();
            for (var i = 0; i < _materials.Length; i++) {
                _logger?.Debug( _materials[i] );
                materials.Add( i, _materials[i] );
            }
            _editor.SetMaterials( materials );

            var attributes = new Dictionary<int, IDictionary<int, IList<string>>>();
            var submeshIdx = 0;

            foreach (var (k,v) in _editor.Attributes) {
                attributes.Add( k, new Dictionary<int, IList<string>>() );
                foreach (var (k2,v2) in v) {
                    _logger?.Debug( $"{k}, {k2}: {_attributes[submeshIdx]}" );
                    var attrList = _attributes[submeshIdx].Split( "," ).ToList();
                    attributes[k].Add( k2, attrList );
                    submeshIdx++;
                }
            }
            _editor.SetAttributes( attributes );

            return _editor.Rebuild();
        }
    }
}
