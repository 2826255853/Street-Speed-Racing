using System;
using System.Collections.Generic;
using UnityEngine;

public class RaceCarVisual
{
    private const float WheelRadius = 0.325f;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private readonly Transform root;
    private readonly List<Transform> wheels = new List<Transform>();
    private readonly List<Quaternion> wheelRestRotations = new List<Quaternion>();

    public Transform Root { get { return root; } }

    public RaceCarVisual(string name, Color color, float laneX, Transform parent)
    {
        // The variant includes the linked art asset in builds and dynamically created races.
        GameObject prefab = Resources.Load<GameObject>("Vehicles/RaceSedan");
        if (prefab == null)
        {
            throw new InvalidOperationException("Missing vehicle prefab: Resources/Vehicles/RaceSedan");
        }

        root = UnityEngine.Object.Instantiate(prefab, parent).transform;
        root.name = name;
        root.position = new Vector3(laneX, 0f, 0f);
        root.rotation = Quaternion.identity;

        // Tint only the paint slot on every LOD, preserving the shared source materials.
        var paint = new MaterialPropertyBlock();
        paint.SetColor(BaseColor, color);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                if (materials[slot] != null && materials[slot].name == "Sedan01_SilverPaint")
                {
                    renderer.SetPropertyBlock(paint, slot);
                }
            }
        }

        // Animate all LODs so changing detail level never resets the wheel pose.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Wheel_FL" || child.name == "Wheel_FR" ||
                child.name == "Wheel_RL" || child.name == "Wheel_RR")
            {
                wheels.Add(child);
                wheelRestRotations.Add(child.localRotation);
            }
        }
    }

    public void SetDistance(float distance)
    {
        root.position = new Vector3(root.position.x, root.position.y, distance);
        float angle = Mathf.Repeat(distance / WheelRadius * Mathf.Rad2Deg, 360f);
        Quaternion roll = Quaternion.AngleAxis(angle, Vector3.right);
        for (int i = 0; i < wheels.Count; i++)
        {
            wheels[i].localRotation = wheelRestRotations[i] * roll;
        }
    }
}
