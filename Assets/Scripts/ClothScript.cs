using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Spring
{
    public Spring(int fst, int snd, float length, float spring=10f, float damp=-10f)
    {
        v1 = fst;
        v2 = snd;
        restLength = length;
        springFactor = spring;
        dampingFactor = damp;
    }
    public int v1, v2;
    public float restLength;
    public float springFactor;
    public float dampingFactor; 
}

public class ClothScript : MonoBehaviour
{
    public float distance = 1f;
    public int width = 11;
    public int height = 11;
    public float invmass = 100f;
    public Vector3 initialPos = new Vector3(-5, 15, 0);
    private Vector3[] velocities;
    private Spring[] springs;

    void buildSprings()
    {
        springs = new Spring[height*(width-1) + (height-1)*width + 2*((height-1)*(width-1))];
        int idx = 0;
        // horizontal springs
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < (width - 1); x++)
            {
                springs[idx++] = new Spring(y * width + x, y * width + x + 1, 1f);
            }
        }
        // vertical springs
        for (int y = 0; y < (height-1); y++) {
            for (int x = 0; x < width; x++)
            {
                springs[idx++] = new Spring(y * width + x, (y+1) * width + x, 1f);
            }
        }
        // diagonal springs
        for (int y=0; y<(height-1); y++)
        {
            for (int x = 0; x < (width-1); x++)
            {
                // top left to bottom right
                springs[idx++] = new Spring(y * width + x, (y+1) * width + x+1, Mathf.Sqrt(2)); 
                // bottom left to top right
                springs[idx++] = new Spring((y+1) * width + x, y * width + x+1, Mathf.Sqrt(2));
            }
        }
        Debug.Assert(springs.Length == idx);
    }

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

        buildSprings();
    }

    void Update()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        //if changing mesh.triangles (when tearing) call mesh.Clear() first

        //TODO: collide with floor (and sphere later)

        Vector3[] forces = new Vector3[width * height];
        for (int i = 0; i < (width * height); i++)
        {
            forces[i] = new Vector3(0, 0, 0);
            // gravity 
            if (i != 0 && i != (width - 1))
            {
                forces[i].y -= 9.81f/invmass;
            }

            // velocity damping
            forces[i] -= 0.2f * velocities[i];
        }

        //solve springs with Linear Strain model (Hooke's Law)
        for (int i=0; i<springs.Length; i++)
        {
            Vector3 dpos = vertices[springs[i].v1] - vertices[springs[i].v2];
            Vector3 dvel = velocities[springs[i].v1] - velocities[springs[i].v2];
            float dist = dpos.magnitude;
            float spring = -springs[i].springFactor * (dist - springs[i].restLength);
            float damp = springs[i].dampingFactor * (Vector3.Dot(dvel, dpos) / dist);
            Vector3 force = (spring + damp) * dpos.normalized;
            if(springs[i].v1 != 0 && springs[i].v1 != (width - 1))
            {
                forces[springs[i].v1] += force;
            }
            if(springs[i].v2 != 0 && springs[i].v2 != (width - 1))
            {
                forces[springs[i].v2] -= force;
            }
        }

        // explicit euler
        for (int i = 0; i < (width * height); i++)
        {
            Vector3 prev = velocities[i];
            velocities[i] += forces[i] * Time.deltaTime * invmass;
            vertices[i] += prev * Time.deltaTime;

            // ground plane
            //vertices[i].y = Mathf.Max(0, vertices[i].y);
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
