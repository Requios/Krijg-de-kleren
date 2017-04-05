using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClothScript : MonoBehaviour
{
    public float distance = 1f;
    public int width = 11;
    public int height = 11;
    public float springFactor = 10f;
    public float dampingFactor= 10f;
    public float invmass = 1f;
    public Vector3 initialPos = new Vector3(-5, 12, 0);
    private Vector3[] velocities;

    void Start()
    {
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        //Create vertices
        Vector3[] vertices = new Vector3[width * height];
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                vertices[i * width + j] = new Vector3(j, -i, 0) + initialPos;
            }
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        velocities = new Vector3[width * height];

        //Create triangles
        //Two triangles between square of vertices
        int[] triangles = new int[(width - 1) * (height - 1) * 2 * 3];
        for (int i = 0; i < height - 1; i++)
        {
            for (int j = 0; j < width - 1; j++)
            {
                int vertIndex = i * width + j;
                int triIndex = (i * (width - 1) + j) * 6;
                triangles[triIndex] = vertIndex;
                triangles[triIndex + 1] = vertIndex + 1;
                triangles[triIndex + 2] = vertIndex + width;

                triangles[triIndex + 3] = vertIndex + 1;
                triangles[triIndex + 4] = vertIndex + width + 1;
                triangles[triIndex + 5] = vertIndex + width;
            }
        }
        mesh.triangles = triangles;

        //Create normals (per vertex)
        Vector3[] normals = new Vector3[width * height];
        for (int i = 0; i < width * height; i++)
        {
            normals[i] = Vector3.back;
        }
        mesh.normals = normals;
    }

    // Linear Strain model (Hooke's Law)
    Vector3 dvel(Vector3 r, Vector3 v)
    {
        float spring = springFactor * (r.magnitude - distance);
        float damp = dampingFactor * Vector3.Dot(v, r.normalized);
        Vector3 f = r.normalized * (spring + damp);
        Vector3 a = f * invmass;
        Vector3 vel = a * Time.deltaTime;
        Debug.LogFormat("spring:{0} damp:{1} f:{2} a:{3} v:{4}", spring, damp, f, a, v);
        return vel;
    }
 
    void Update()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        //if changing mesh.triangles (when tearing) call mesh.Clear() first

        //gravity
        for (int i = 0; i < velocities.Length; i++)
        {
            if (i != 0 && i != (width - 1))
            {
                velocities[i].y -= 9.81f * Time.deltaTime;
            }
        }

        //apply velocities
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] += velocities[i] * Time.deltaTime;
        }

        //collide with floor (and sphere later)

        //solve springs
        for (int y = 0; y < width-1; y++)
        {
            for (int x = 0; x < height-1; x++)
            {
                int w = width - 1;
                int h = height - 1;
                if (x == 0 && y == 0) continue;
                if (x == w && y == 0) continue;
                int i = y * width + x;
                if (x > 0) velocities[i] += dvel(vertices[i-1]   - vertices[i], 
                                                 velocities[i-1] - velocities[i]);
                if (x < w) velocities[i] += dvel(vertices[i+1]   - vertices[i],
                                                 velocities[i+1] - velocities[i]);
                if (y > 0) velocities[i] += dvel(vertices[i-width]   - vertices[i], 
                                                 velocities[i-width] - velocities[i]);
                if (y < h) velocities[i] += dvel(vertices[i+width]   - vertices[i], 
                                                 velocities[i+width] - velocities[i]);
            }
        }
        

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
