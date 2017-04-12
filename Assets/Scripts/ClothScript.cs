using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Spring
{
    public Spring(int fst, int snd, float length, float spring=1000f, float damp=-10f)
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
    public float deltaTime = 1f/30f;
    public Vector3 initialPos = new Vector3(-5, 15, 0);
    public Vector3 windVector;
    private Vector3[] prevPos;
    private Vector3[] currPos;
    private Spring[] springs;

    Vector3 velocity(int i)
    {
        return (currPos[i] - prevPos[i]) / deltaTime;
    }

    void buildSprings()
    {
        float springDistance = distance * 1.0f;
        springs = new Spring[height*(width-1) + (height-1)*width + 2*((height-1)*(width-1))];
        int idx = 0;
        // horizontal springs
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < (width - 1); x++)
            {
                springs[idx++] = new Spring(y * width + x, y * width + x + 1, springDistance);
            }
        }
        // vertical springs
        for (int y = 0; y < (height-1); y++) {
            for (int x = 0; x < width; x++)
            {
                springs[idx++] = new Spring(y * width + x, (y+1) * width + x, springDistance);
            }
        }
        // diagonal springs
        for (int y=0; y<(height-1); y++)
        {
            for (int x = 0; x < (width-1); x++)
            {
                // top left to bottom right
                springs[idx++] = new Spring(y * width + x, (y + 1) * width + x + 1, Mathf.Sqrt(springDistance * springDistance+ springDistance*springDistance));
                // bottom left to top right
                springs[idx++] = new Spring((y + 1) * width + x, y * width + x + 1, Mathf.Sqrt(springDistance * springDistance + springDistance*springDistance));
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
                vertices[i * width + j] = new Vector3(j * distance, 0, i * distance) + initialPos;
            }
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        prevPos = new Vector3[width * height];
        for(int i=0; i<vertices.Length; i++)
        {
            prevPos[i] = vertices[i];
        }

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

    bool isFixed(int i)
    {
        int c = Math.Max(1, width / 3);
        return (i < c) || ((i >= (width - c)) && (i < width));
    }

    void Update()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        currPos = mesh.vertices;
        int[] triangles = mesh.triangles;
        //if changing mesh.triangles (when tearing) call mesh.Clear() first

        //TODO: collide with floor (and sphere later)

        Vector3[] forces = new Vector3[width * height];
        for (int i = 0; i < (width * height); i++)
        {
            forces[i] = new Vector3(0, 0, 0);
            if(!isFixed(i))
            {
                // gravity 
                forces[i].y -= 9.81f/invmass;
                // wind
                forces[i] += windVector + windVector.normalized * (0.5f * Mathf.Sin(0.01f*Time.time));
            }


            // velocity damping
            forces[i] -= 2.0f * velocity(i);
        }

        //solve springs with Linear Strain model (Hooke's Law)
        for (int i=0; i<springs.Length; i++)
        {
            Vector3 dpos = currPos[springs[i].v1] - currPos[springs[i].v2];
            Vector3 dvel = velocity(springs[i].v1) - velocity(springs[i].v2);
            float dist = dpos.magnitude;
            float spring = -springs[i].springFactor * (dist - springs[i].restLength);
            float damp = springs[i].dampingFactor * (Vector3.Dot(dvel, dpos) / dist);
            Vector3 force = (spring + damp) * dpos.normalized;
            if (!isFixed(springs[i].v1))
            {
                forces[springs[i].v1] += force;
            }
            if (!isFixed(springs[i].v2))
            {
                forces[springs[i].v2] -= force;
            }
        }

        // verlet integration
        for (int i = 0; i < (width * height); i++)
        {
            Vector3 tmp = currPos[i];
            currPos[i] = currPos[i] + (currPos[i] - prevPos[i]) + forces[i]*(deltaTime * deltaTime * invmass);
            prevPos[i] = tmp;

            // ground plane
            //vertices[i].y = Mathf.Max(0, vertices[i].y);
        }


        mesh.vertices = currPos;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
