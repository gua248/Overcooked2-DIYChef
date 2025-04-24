/*
 * !!!! Mirror along the x-axis, 
 * !!!! including flipping the vertices, normals 
 * !!!! and triangle vertex index order
 */

/* This version of ObjImporter first reads through the entire file, getting a count of how large
 * the final arrays will be, and then uses standard arrays for everything (as opposed to ArrayLists
 * or any other fancy things). 
 */

using UnityEngine;
using System.Collections.Generic;
using System.IO;
//using System.Threading; // async sometimes causes crash

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS0649 // Field never assigned

public static class ObjImporter
{
    private struct IntVector3
    {
        public int i, j, k;
    }

    private struct MeshStruct
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public Vector2[] uv1;
        public Vector2[] uv2;
        public int[] triangles;
        public int[] faceVerts;
        public int[] faceUVs;
        public IntVector3[] faceData;
        public string name;
    }

    public static Mesh ImportFile(string filePath)
    {
        Mesh mesh = new Mesh();
        ImportFile(filePath, ref mesh);
        return mesh;
    }

    // Use this for initialization
    static void ImportFile(string filePath, ref Mesh mesh)
    {
        MeshStruct newMesh = CreateMeshStruct(filePath);

        Vector3[] newVerts = new Vector3[newMesh.faceData.Length];
        Vector2[] newUVs = new Vector2[newMesh.faceData.Length];
        Vector3[] newNormals = new Vector3[newMesh.faceData.Length];
        int i = 0;
        /* The following foreach loops through the facedata and assigns the appropriate vertex, uv, or normal
         * for the appropriate Unity mesh array.
         */
        foreach (IntVector3 v in newMesh.faceData)
        {
            newVerts[i] = newMesh.vertices[v.i - 1];
            if (v.j >= 1)
                newUVs[i] = newMesh.uv[v.j - 1];

            if (v.k >= 1)
                newNormals[i] = newMesh.normals[v.k - 1];

            i++;
        }

        mesh.vertices = newVerts;
        mesh.uv = newUVs;
        mesh.normals = newNormals;
        mesh.triangles = newMesh.triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        /*
         * https://docs.unity3d.com/ScriptReference/Mesh-tangents.html
         * "You should calculate tangents yourself if you plan to use bump-mapped shaders on the Mesh." 
         * "Assign tangents after assigning normals or using RecalculateNormals."
         */
    }

    private static MeshStruct CreateMeshStruct(string filename)
    {
        int triangles = 0;
        int vertices = 0;
        int vt = 0;
        int vn = 0;
        int face = 0;
        MeshStruct mesh = new MeshStruct();
        StreamReader stream = File.OpenText(filename);
        string entireText = stream.ReadToEnd();
        stream.Close();
        using (StringReader reader = new StringReader(entireText))
        {
            string currentText = reader.ReadLine();
            char[] splitIdentifier = { ' ' };
            string[] brokenString;
            while (currentText != null)
            {
                if (currentText.StartsWith("f "))
                {
                    currentText = currentText.Trim();                           //Trim the current line
                    brokenString = currentText.Split(splitIdentifier, 100);      //Split the line into an array, separating the original line by blank spaces
                    face = face + brokenString.Length - 1;
                    triangles += 3 * (brokenString.Length - 3); /*brokenString.Length is 4 or greater since a face must have at least
                                                                                     3 vertices.  For each additional vertice, there is an additional
                                                                                     triangle in the mesh (hence this formula).*/
                }
                else if (currentText.StartsWith("v "))
                    vertices++;
                else if (currentText.StartsWith("vt "))
                    vt++;
                else if (currentText.StartsWith("vn "))
                    vn++;

                currentText = reader.ReadLine();
                if (currentText != null)
                {
                    currentText = currentText.Replace("  ", " ");
                }
            }
        }
        mesh.triangles = new int[triangles];
        mesh.vertices = new Vector3[vertices];
        mesh.uv = new Vector2[vt];
        mesh.normals = new Vector3[vn];
        mesh.faceData = new IntVector3[face];

        PopulateMeshStruct(ref mesh, entireText);

        return mesh;
    }

    private static void PopulateMeshStruct(ref MeshStruct mesh, string entireText)
    {
        using (StringReader reader = new StringReader(entireText))
        {
            string currentText = reader.ReadLine();

            char[] splitIdentifier = { ' ' };
            char[] splitIdentifier2 = { '/' };
            string[] brokenString;
            string[] brokenBrokenString;
            int f = 0;
            int f2 = 0;
            int v = 0;
            int vn = 0;
            int vt = 0;
            int vt1 = 0;
            int vt2 = 0;
            while (currentText != null)
            {
                if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ") &&
                    !currentText.StartsWith("vn ") && !currentText.StartsWith("g ") && !currentText.StartsWith("usemtl ") &&
                    !currentText.StartsWith("mtllib ") && !currentText.StartsWith("vt1 ") && !currentText.StartsWith("vt2 ") &&
                    !currentText.StartsWith("vc ") && !currentText.StartsWith("usemap "))
                {
                    currentText = reader.ReadLine();
                    if (currentText != null)
                    {
                        currentText = currentText.Replace("  ", " ");
                    }
                }
                else
                {
                    currentText = currentText.Trim();
                    brokenString = currentText.Split(splitIdentifier, 100);
                    switch (brokenString[0])
                    {
                        case "g":
                            break;
                        case "usemtl":
                            break;
                        case "usemap":
                            break;
                        case "mtllib":
                            break;
                        case "v":
                            mesh.vertices[v] = new Vector3(
                                -System.Convert.ToSingle(brokenString[1]), 
                                System.Convert.ToSingle(brokenString[2]),
                                System.Convert.ToSingle(brokenString[3]));
                            v++;
                            break;
                        case "vt":
                            mesh.uv[vt] = new Vector2(
                                System.Convert.ToSingle(brokenString[1]), 
                                System.Convert.ToSingle(brokenString[2]));
                            vt++;
                            break;
                        case "vt1":
                            mesh.uv[vt1] = new Vector2(
                                System.Convert.ToSingle(brokenString[1]), 
                                System.Convert.ToSingle(brokenString[2]));
                            vt1++;
                            break;
                        case "vt2":
                            mesh.uv[vt2] = new Vector2(
                                System.Convert.ToSingle(brokenString[1]), 
                                System.Convert.ToSingle(brokenString[2]));
                            vt2++;
                            break;
                        case "vn":
                            mesh.normals[vn] = new Vector3(
                                -System.Convert.ToSingle(brokenString[1]), 
                                System.Convert.ToSingle(brokenString[2]),
                                System.Convert.ToSingle(brokenString[3]));
                            vn++;
                            break;
                        case "vc":
                            break;
                        case "f":

                            int j = 1;
                            List<int> intArray = new List<int>();
                            while (j < brokenString.Length && ("" + brokenString[j]).Length > 0)
                            {
                                IntVector3 temp = new IntVector3();
                                brokenBrokenString = brokenString[j].Split(splitIdentifier2, 3);    //Separate the face into individual components (vert, uv, normal)
                                temp.i = System.Convert.ToInt32(brokenBrokenString[0]);
                                if (brokenBrokenString.Length > 1)                                  //Some .obj files skip UV and normal
                                {
                                    if (brokenBrokenString[1] != "")                                    //Some .obj files skip the uv and not the normal
                                    {
                                        temp.j = System.Convert.ToInt32(brokenBrokenString[1]);
                                    }
                                    temp.k = System.Convert.ToInt32(brokenBrokenString[2]);
                                }
                                j++;

                                mesh.faceData[f2] = temp;
                                intArray.Add(f2);
                                f2++;
                            }
                            j = 1;
                            while (j + 2 < brokenString.Length)     //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                            {
                                mesh.triangles[f] = intArray[0];
                                f++;
                                mesh.triangles[f] = intArray[j + 1];
                                f++;
                                mesh.triangles[f] = intArray[j];
                                f++;

                                j++;
                            }
                            break;
                    }
                    currentText = reader.ReadLine();
                    if (currentText != null)
                    {
                        currentText = currentText.Replace("  ", " ");       //Some .obj files insert double spaces, this removes them.
                    }
                }
            }
        }
    }
}
