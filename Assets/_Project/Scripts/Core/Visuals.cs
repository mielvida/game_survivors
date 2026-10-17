using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    // Prototype visuals built from primitives. Replaced by real 3D assets later.
    public static class Visuals
    {
        static Shader _shader;
        static readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        public static Material Mat(Color c)
        {
            Material m;
            if (_materials.TryGetValue(c, out m) && m != null) return m;
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit");
                if (_shader == null) _shader = Shader.Find("Standard");
                if (_shader == null) _shader = Shader.Find("Sprites/Default");
            }
            m = new Material(_shader);
            m.color = c;
            _materials[c] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Color color, Vector3 scale, Transform parent)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            g.GetComponent<Renderer>().sharedMaterial = Mat(color);
            g.transform.SetParent(parent, false);
            g.transform.localScale = scale;
            return g;
        }

        public static void ClearCache()
        {
            _materials.Clear();
        }
    }

    public sealed class Pool
    {
        readonly Stack<GameObject> _free = new Stack<GameObject>();
        readonly System.Func<GameObject> _make;

        public Pool(System.Func<GameObject> make)
        {
            _make = make;
        }

        public GameObject Get()
        {
            var g = _free.Count > 0 ? _free.Pop() : _make();
            g.SetActive(true);
            return g;
        }

        public void Release(GameObject g)
        {
            g.SetActive(false);
            _free.Push(g);
        }
    }
}
