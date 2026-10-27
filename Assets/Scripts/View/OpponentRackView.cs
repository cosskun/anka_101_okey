using System.Collections.Generic;
using UnityEngine;

namespace Okey101.View
{
    /// <summary>
    /// Diğer 3 oyuncunun (Botların) masada duran sembolik ahşap ıstakasını ve
    /// kapalı duran taşlarını (arkası dönük taş sırtları) görselleştiren bileşen.
    /// </summary>
    public class OpponentRackView : MonoBehaviour
    {
        [SerializeField] private int playerIndex;
        [SerializeField] private Transform tileContainer;
        [SerializeField] private GameObject closedTilePrefab;

        private List<GameObject> _spawnedTileBacks = new List<GameObject>();

        public void UpdateTileCount(int currentTileCount)
        {
            if (tileContainer == null || closedTilePrefab == null) return;

            // Mevcut taş sırtlarını güncelle
            while (_spawnedTileBacks.Count < currentTileCount)
            {
                var tileBack = Instantiate(closedTilePrefab, tileContainer);
                _spawnedTileBacks.Add(tileBack);
            }

            while (_spawnedTileBacks.Count > currentTileCount)
            {
                var last = _spawnedTileBacks[_spawnedTileBacks.Count - 1];
                _spawnedTileBacks.RemoveAt(_spawnedTileBacks.Count - 1);
                if (last != null) Destroy(last);
            }
        }
    }
}
