using UnityEngine;

namespace Okey101.View
{
    /// <summary>
    /// Çift sıralı ıstakadaki her bir kare/slot hücresini temsil eden bileşen.
    /// Sürükle-bırak hedefi (Drop Target) olarak çalışır.
    /// </summary>
    public class RackSlotView : MonoBehaviour
    {
        public int Row { get; set; }
        public int Col { get; set; }
    }
}
