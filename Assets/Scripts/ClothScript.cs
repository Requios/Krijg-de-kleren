﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

class Spring
{
    public Spring(int fst, int snd, float length)
    {
        v1 = fst;
        v2 = snd;
        restLength = length;
    }
    public int v1, v2;
    public float restLength;
}

public class ClothScript : MonoBehaviour
{
    [Header("Creation")]
    [Tooltip("Distance between cloth particles")]
    public float distance = 1f;
    [Tooltip("Number of particles horizontally")]
    public int width = 11;
    [Tooltip("Number of particles vertically")]
    public int height = 11;
    public Vector3 initialPos = new Vector3(-5, 15, 0);
    [Tooltip("Whether to start flat")]
    public bool startHorizontal = true;
    public float stretchedStart = 1f;

    [Header("Behavior")]
    public Fixed fixedVertices = Fixed.topCorners;
    public float invmass = 1f;
    [Tooltip("How strong the forces of the spring are. Higher is stronger.")]
    public float springFactor = 100f;
    [Tooltip("How strong the springs are dampened (preventing infinite oscillation). Higher is stronger.")]
    public float dampingFactor = -10f;
    [Tooltip("Velocity damping. Lower is stronger.")]
    [Range(0,1)]
    public float airResistance = 0.5f;
    public Vector3 windVector;
    [Tooltip("How much to stay away from collisions to prevent penetration")]
    public float collisionDelta = 0.1f;

    private Vector3[] prevPos;
    private Vector3[] currPos;
    private Spring[] springs;

    private Vector3 spherePos;
    private float sphereRadius;

    private float windRandom;

    public enum Fixed
    {
        topCorners,
        topLine,
        leftCorners,
        leftLine
    }

    Vector3 velocity(int i)
    {
        return (currPos[i] - prevPos[i]) / Time.fixedDeltaTime;
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
                if (startHorizontal)
                    vertices[i * width + j] = new Vector3(j * distance * stretchedStart, 0, i * distance * stretchedStart) + initialPos;
                else
                    vertices[i * width + j] = new Vector3(j * distance * stretchedStart, -i * distance * stretchedStart, 0) + initialPos;
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

        //Create uv (texture) coordinates
        Vector2[] uv = new Vector2[width * height];
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                uv[i * width + j] = new Vector2(j / (float) (width-1), 1 - (i / (float) (height-1)));
            }
        }
        mesh.uv = uv;

        //Find sphere
        GameObject sphere = GameObject.Find("Sphere");
        if (sphere)
        {
            spherePos = sphere.transform.position;
            sphereRadius = sphere.transform.localScale.x / 2f;
        }

        buildSprings();
    }

    bool isFixed(int i)
    {
        switch (fixedVertices)
        {
            case Fixed.topCorners:
                return i == 0 || i == width - 1;
            case Fixed.topLine:
                return i < width;
            case Fixed.leftCorners:
                return i == 0 || i == width * (height - 1);
            case Fixed.leftLine:
                return i % width == 0;
        }
        return false;
    }

    void FixedUpdate()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        currPos = mesh.vertices;
        int[] triangles = mesh.triangles;
        //if changing mesh.triangles (when tearing) call mesh.Clear() first

        Vector3[] forces = new Vector3[width * height];
        for (int i = 0; i < (width * height); i++)
        {
            forces[i] = new Vector3(0, 0, 0);
            if(!isFixed(i))
            {
                // gravity 
                forces[i].y -= 9.81f/invmass;
                // wind
                //forces[i] += windVector + windVector.normalized * (0.5f * Mathf.Sin(Time.time));
                if (Time.frameCount % 30 == 0) //pick a new value every so many frames
                    windRandom = Random.Range(0.2f, 1f);
                forces[i] += windVector * windRandom * Random.Range(0.8f, 1.2f);
            }
            
            // velocity damping
            forces[i] -= airResistance * velocity(i);
        }

        //solve springs with Linear Strain model (Hooke's Law)
        for (int i=0; i<springs.Length; i++)
        {
            Vector3 dpos = currPos[springs[i].v1] - currPos[springs[i].v2];
            Vector3 dvel = velocity(springs[i].v1) - velocity(springs[i].v2);
            float dist = dpos.magnitude;
            float spring = -springFactor * (dist - springs[i].restLength);
            float damp = dampingFactor * (Vector3.Dot(dvel, dpos) / dist);
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
            currPos[i] = currPos[i] + (currPos[i] - prevPos[i]) + forces[i]*(Time.fixedDeltaTime * Time.fixedDeltaTime * invmass);
            prevPos[i] = tmp;

            // collision
            // ground plane
            currPos[i].y = Mathf.Max(collisionDelta, currPos[i].y);

            // sphere
            if ((currPos[i] - spherePos).magnitude < sphereRadius + collisionDelta)
            {
                currPos[i] = spherePos + (currPos[i] - spherePos).normalized * (sphereRadius + collisionDelta);
            }
        }
        
        mesh.vertices = currPos;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
