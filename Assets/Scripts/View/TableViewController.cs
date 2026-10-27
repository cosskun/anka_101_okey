using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Data;
using Okey101.Core.Enums;
using Okey101.Core.Logic;

namespace Okey101.View
{
    /// <summary>
    /// Masadaki 3D/2D ortamı, çekme destesini, gösterge taşını,
    /// 4 oyuncunun attığı taş yığınlarını ve ortada açılan per gruplarını yönetir.
    /// </summary>
    public class TableViewController : MonoBehaviour
    {
        [Header("Çekme Destesi ve Gösterge")]
        [SerializeField] private TileView indicatorTileView;
        [SerializeField] private Text remainingDeckCountText;
        [SerializeField] private GameObject drawDeckObject;

        [Header("4 Oyuncu Atılan Taş Alanları (Discard Piles)")]
        [SerializeField] private Transform[] playerDiscardPositions; // 0: Alt(Kullanıcı), 1: Sağ, 2: Üst, 3: Sol
        [SerializeField] private GameObject tilePrefab;

        [Header("Masadaki Açık Perler Alanı")]
        [SerializeField] private Transform tableSeriesContainer;
        [SerializeField] private GameObject perGroupPrefab;

        [Header("Sıra Göstergesi")]
        [SerializeField] private GameObject[] playerTurnHighlights; // 0..3 aktif oyuncu vurgusu

        private List<GameObject> _spawnedPerObjects = new();
        private List<GameObject>[] _spawnedDiscardTiles = new List<GameObject>[4];

        public event Action OnDrawDeckClicked;
        public event Action<int> OnDiscardPileClicked; // (oyuncu indeksi)
        public event Action<PerGroup> OnTablePerClicked;

        private void Awake()
        {
            for (int i = 0; i < 4; i++)
            {
                _spawnedDiscardTiles[i] = new List<GameObject>();
            }
        }

        public void SetIndicator(TileData indicatorTile, int remainingDeckCount)
        {
            if (indicatorTileView != null)
            {
                indicatorTileView.BindData(indicatorTile);
            }

            UpdateDeckCount(remainingDeckCount);
        }

        public void UpdateDeckCount(int count)
        {
            if (remainingDeckCountText != null)
            {
                remainingDeckCountText.text = count.ToString();
            }
        }

        public void HighlightTurn(int activePlayerIndex)
        {
            if (playerTurnHighlights == null) return;

            for (int i = 0; i < playerTurnHighlights.Length; i++)
            {
                if (playerTurnHighlights[i] != null)
                {
                    playerTurnHighlights[i].SetActive(i == activePlayerIndex);
                }
            }
        }

        public void AddDiscardTile(int playerIndex, TileData tile)
        {
            if (playerIndex < 0 || playerIndex >= playerDiscardPositions.Length || tilePrefab == null) return;

            var container = playerDiscardPositions[playerIndex];
            var tileObj = Instantiate(tilePrefab, container);
            var view = tileObj.GetComponent<TileView>();
            view.BindData(tile);

            _spawnedDiscardTiles[playerIndex].Add(tileObj);
        }

        public void RemoveLastDiscardTile(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _spawnedDiscardTiles.Length) return;
            var list = _spawnedDiscardTiles[playerIndex];
            if (list.Count > 0)
            {
                var lastObj = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                if (lastObj != null) Destroy(lastObj);
            }
        }

        public void RefreshAllTableMelds(IEnumerable<PerGroup> seriesMelds, IEnumerable<PerGroup> pairMelds)
        {
            foreach (var obj in _spawnedPerObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedPerObjects.Clear();

            if (seriesMelds != null)
            {
                foreach (var per in seriesMelds)
                {
                    RenderOpenedPer(per);
                }
            }

            if (pairMelds != null)
            {
                foreach (var pair in pairMelds)
                {
                    RenderOpenedPer(pair);
                }
            }
        }

        public void ClearTable()
        {
            foreach (var obj in _spawnedPerObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedPerObjects.Clear();

            for (int i = 0; i < 4; i++)
            {
                foreach (var d in _spawnedDiscardTiles[i])
                {
                    if (d != null) Destroy(d);
                }
                _spawnedDiscardTiles[i].Clear();
            }
        }

        public void RenderOpenedPer(PerGroup perGroup)
        {
            if (tableSeriesContainer == null || perGroupPrefab == null || tilePrefab == null) return;

            var groupObj = Instantiate(perGroupPrefab, tableSeriesContainer);
            _spawnedPerObjects.Add(groupObj);

            foreach (var tile in perGroup.Tiles)
            {
                var tileObj = Instantiate(tilePrefab, groupObj.transform);
                var view = tileObj.GetComponent<TileView>();
                view.BindData(tile);
            }
        }

        public void OnDrawDeckButton()
        {
            OnDrawDeckClicked?.Invoke();
        }

        public void OnDiscardPileButton(int playerIndex)
        {
            OnDiscardPileClicked?.Invoke(playerIndex);
        }
    }
}
