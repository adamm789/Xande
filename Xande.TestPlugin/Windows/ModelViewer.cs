using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xande.GltfImporter;

namespace Xande.TestPlugin.Windows {
    internal class ModelViewer : IDisposable {
        public MdlFileBuilder? Mdl = null;
        public ModelViewer( MdlFileBuilder? mdl) {
            Mdl = mdl;
        }

        public void Draw() {
            if ( Mdl == null) {
                ImGui.Text( $"Provide mdl." );
                return;
            }
            if ( Mdl.HasSkeleton) {
                ImGui.Text( "Has skeleton" );
            }
            else {
                ImGui.Text( "Does not have skeleton" );
            }

            foreach (var mesh in Mdl._meshBuilders) {
                foreach (var submesh in mesh.Submeshes) {
                    var attr = String.Join( "\n", submesh.Attributes );
                    SubmeshAttributes.Add( submesh, attr );
                    //ImGui.InputText($"{submesh.Name}", SubmeshAttributes[submesh], 1024 );
                }
                ImGui.Separator();
            }
        }

        private Dictionary<SubmeshBuilder, string> SubmeshAttributes = new();

        public void Dispose() {
            
        }
    }
}
