using UnityEngine;

public class RaceCarVisual
{
    private readonly Transform root;

    public RaceCarVisual(string name, Color color, float laneX)
    {
        root = new GameObject(name).transform;

        Transform body = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        body.name = "Body";
        body.SetParent(root, false);
        body.localPosition = new Vector3(0f, 0.65f, -0.2f);
        body.localScale = new Vector3(2.2f, 0.55f, 4.3f);
        SetMaterial(body.gameObject, color);

        Transform cabin = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        cabin.name = "Cabin";
        cabin.SetParent(root, false);
        cabin.localPosition = new Vector3(0f, 1.08f, -0.35f);
        cabin.localScale = new Vector3(1.5f, 0.5f, 1.9f);
        SetMaterial(cabin.gameObject, Color.Lerp(color, Color.black, 0.5f));

        CreateWheel(new Vector3(-0.82f, 0.35f, -1.4f), root);
        CreateWheel(new Vector3(0.82f, 0.35f, -1.4f), root);
        CreateWheel(new Vector3(-0.82f, 0.35f, 1.25f), root);
        CreateWheel(new Vector3(0.82f, 0.35f, 1.25f), root);
        root.position = new Vector3(laneX, 0f, 0f);
    }

    public void SetDistance(float distance)
    {
        root.position = new Vector3(root.position.x, root.position.y, distance);
    }

    private static void CreateWheel(Vector3 localPosition, Transform parent)
    {
        Transform wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        wheel.name = "Wheel";
        wheel.SetParent(parent, false);
        wheel.localPosition = localPosition;
        wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.localScale = new Vector3(0.34f, 0.19f, 0.34f);
        SetMaterial(wheel.gameObject, new Color(0.07f, 0.07f, 0.08f));
    }

    private static void SetMaterial(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        renderer.sharedMaterial = material;
    }
}
