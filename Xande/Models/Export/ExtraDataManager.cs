using Xande.ModelData.Models;
using SharpGLTF.IO;

namespace Xande.Models.Export;

public class ExtraDataManager {
    private readonly Dictionary< string, object > _extraData = new();

    public void AddShapeNames( IEnumerable< Shape > shapes ) {
        _extraData.Add( "targetNames", shapes.Select( s => s.Name ).ToArray() );
    }

    public JsonContent Serialize() => JsonContent.CreateFrom( _extraData );
}