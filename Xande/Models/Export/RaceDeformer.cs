﻿using System.Numerics;
using SharpGLTF.Scenes;
using Xande.Files;

namespace Xande.Models.Export;

/// <summary>Calculates deformations from a PBD file.</summary>
public class RaceDeformer {
    public readonly  PbdFile                           PbdFile;
    private readonly Dictionary< string, NodeBuilder > _boneMap;

    public RaceDeformer( PbdFile pbd, Dictionary< string, NodeBuilder > boneMap ) {
        PbdFile  = pbd;
        _boneMap = boneMap;
    }

    /// <summary>Gets the parent of a given race code.</summary>
    public ushort? GetParent( ushort raceCode ) {
        // TODO: npcs
        // Annoying special cases
        if( raceCode == 1201 ) return 1101; // Lalafell F -> Lalafell M
        if( raceCode == 0201 ) return 0101; // Midlander F -> Midlander M
        if( raceCode == 1001 ) return 0201; // Roegadyn F -> Midlander F
        if( raceCode == 0101 ) return null; // Midlander M has no parent

        // First two digits being odd or even can tell us gender
        var isMale = raceCode / 100 % 2 == 1;

        // Midlander M / Midlander F
        return ( ushort )( isMale ? 0101 : 0201 );
    }

    /// <summary>Parses a model path to obtain its race code.</summary>
    public ushort? RaceCodeFromPath( string path ) {
        var fileName = Path.GetFileNameWithoutExtension( path );
        if( fileName[ 0 ] != 'c' ) return null;

        return ushort.Parse( fileName[ 1..5 ] );
    }

    private float[]? ResolveDeformation( PbdFile.Deformer deformer, string name ) {
        // Try and fetch it from the PBD
        var boneNames = deformer.BoneNames;
        var boneIdx   = Array.FindIndex( boneNames, x => x == name );
        if( boneIdx != -1 ) { return deformer.DeformMatrices[ boneIdx ]; }

        // Try and get it from the parent
        var boneNode = _boneMap[ name ];
        if( boneNode.Parent != null ) { return ResolveDeformation( deformer, boneNode.Parent.Name ); }

        // No deformation, just use identity
        return new float[] {
            0, 0, 0, 0, // Translation (vec3 + unused)
            0, 0, 0, 1, // Rotation (vec4)
            1, 1, 1, 0  // Scale (vec3 + unused)
        };
    }

    /// <summary>Deforms a vertex using a deformer.</summary>
    /// <param name="deformer">The deformer to use.</param>
    /// <param name="nameIndex">The index of the bone name in the deformer's bone name list.</param>
    /// <param name="origPos">The original position of the vertex.</param>
    /// <returns>The deformed position of the vertex.</returns>
    public Vector3? DeformVertex( PbdFile.Deformer deformer, int nameIndex, Vector3 origPos ) {
        var boneNames = _boneMap.Keys.ToArray();
        var boneName  = boneNames[ nameIndex ];
        var matrix    = ResolveDeformation( deformer, boneName );
        if( matrix != null ) { return MatrixTransform( origPos, matrix ); }

        return null;
    }

    // Literally ripped directly from xivModdingFramework because I am lazy
    private static Vector3 MatrixTransform( Vector3 vector, float[] transform ) => new(
        vector.X * transform[ 0 ] + vector.Y * transform[ 1 ] + vector.Z * transform[ 2 ] + 1.0f * transform[ 3 ],
        vector.X * transform[ 4 ] + vector.Y * transform[ 5 ] + vector.Z * transform[ 6 ] + 1.0f * transform[ 7 ],
        vector.X * transform[ 8 ] + vector.Y * transform[ 9 ] + vector.Z * transform[ 10 ] + 1.0f * transform[ 11 ]
    );
}