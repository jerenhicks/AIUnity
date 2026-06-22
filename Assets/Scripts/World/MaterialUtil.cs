using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Shared helper for building solid-color materials that render correctly in
    /// both URP and the Built-in pipeline (URP uses _BaseColor, Built-in uses _Color).
    /// </summary>
    public static class MaterialUtil
    {
        public static Material CreateColored(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
