using UnityEditor;
using UnityEngine;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Cajas y barriles del paquete "PSX Style Crates and Barrels", tal cual: provisiones en la
    /// plaza y a la puerta de la vieja panadería. Se apilan de abajo arriba sobre el suelo del valle.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string CarpetaCajas = "Assets/McSteeg/PSX Style Crates and Barrels Pack/Prefabs/";

        static GameObject ModeloCaja(string nombre) => AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaCajas + nombre + ".prefab");

        /// <summary>
        /// Una pila en `p` (mundo): el primero en el suelo y cada uno de los siguientes encima del
        /// anterior, un poco girado. Devuelve false si falta el paquete.
        /// </summary>
        static bool Pila(Transform padre, Valle valle, Vector3 p, float giro, params string[] nombres)
        {
            float suelo = valle != null ? valle.Altura(p.x, p.z) : p.y;
            float altura = suelo;
            bool alguno = false;
            for (int i = 0; i < nombres.Length; i++)
            {
                GameObject modelo = ModeloCaja(nombres[i]);
                if (modelo == null) continue;
                var objeto = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
                objeto.name = nombres[i];
                objeto.transform.rotation = Quaternion.Euler(0f, giro + i * 17f - (i % 2) * 31f, 0f);
                Asentar(objeto, new Vector3(p.x, altura - (i == 0 ? 0.02f : 0f), p.z));
                altura = Medir(objeto).max.y;
                foreach (Transform t in objeto.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                if (objeto.GetComponentInChildren<Collider>() == null) CajaVisible(objeto, 0.95f);
                alguno = true;
            }
            return alguno;
        }

        /// <summary>Provisiones de la plaza: cajas apiladas, barriles y un tonel en su soporte.</summary>
        static void CajasDeLaPlaza(Transform plaza, Valle valle)
        {
            Vector3 c = plaza.position;
            if (ModeloCaja("Crate_1") == null)
            {
                Debug.LogWarning("Nostos: falta el paquete PSX Style Crates and Barrels; la plaza se queda sin cajas.");
                return;
            }
            // El lado de -X y +Z queda libre: ahí se planta el arconte el día del tributo.
            Pila(plaza, valle, c + new Vector3(2.6f, 0f, -4.4f), 12f, "Crate_1", "Crate_2");
            Pila(plaza, valle, c + new Vector3(3.6f, 0f, -3.8f), 40f, "Crate_1_open");
            Pila(plaza, valle, c + new Vector3(1.5f, 0f, -4.9f), 0f, "Barrel_1");
            Pila(plaza, valle, c + new Vector3(-5.1f, 0f, -0.8f), 70f, "Barrel_2_keg");
            Pila(plaza, valle, c + new Vector3(4.7f, 0f, -2.3f), 200f, "Barrel_Stand", "Barrel_2_keg");
            Pila(plaza, valle, c + new Vector3(5.3f, 0f, -0.9f), 160f, "Crate_2", "Crate_lid");
            Pila(plaza, valle, c + new Vector3(-2.4f, 0f, -4.6f), 95f, "Chest_1");
        }

        /// <summary>A la puerta de la vieja panadería (la puerta mira a -Z del modelo).</summary>
        static void CajasDeLaPanaderia(Transform casa, Valle valle)
        {
            if (ModeloCaja("Crate_1") == null) return;
            Pila(casa, valle, casa.TransformPoint(new Vector3(3.3f, 0f, -5.9f)), casa.eulerAngles.y + 8f, "Crate_1", "Crate_1", "Crate_2");
            Pila(casa, valle, casa.TransformPoint(new Vector3(4.3f, 0f, -5.4f)), casa.eulerAngles.y + 50f, "Crate_1_open");
            Pila(casa, valle, casa.TransformPoint(new Vector3(-4.7f, 0f, -5.0f)), casa.eulerAngles.y + 130f, "Barrel_1_open");
            Pila(casa, valle, casa.TransformPoint(new Vector3(5.8f, 0f, 2.4f)), casa.eulerAngles.y + 20f, "Crate_Broken");
        }
    }
}
