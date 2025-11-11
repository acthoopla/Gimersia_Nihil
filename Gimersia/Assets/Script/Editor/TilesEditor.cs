using UnityEditor; // Penting
using UnityEngine;

[CustomEditor(typeof(Tiles))] // Memberi tahu Unity script ini untuk 'Tiles.cs'
public class TilesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Gambar Inspector default (semua variabel publikmu)
        DrawDefaultInspector();

        // Ambil referensi ke script 'Tiles' yang sedang dilihat
        Tiles myScript = (Tiles)target;

        // Hanya tampilkan tombol jika Type-nya adalah SnakeStart
        if (myScript.type == TileType.SnakeStart)
        {
            // Beri spasi sedikit
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Snake Path Generator", EditorStyles.boldLabel);

            // Buat Tombol "Generate"
            if (GUILayout.Button("Generate Snake Path", GUILayout.Height(30)))
            {
                // Panggil fungsi publik di 'Tiles.cs'
                myScript.GenerateSnakePath();
            }

            // Buat Tombol "Clear"
            if (GUILayout.Button("Clear Snake Path"))
            {
                myScript.ClearSnakePath();
            }
        }
    }
}