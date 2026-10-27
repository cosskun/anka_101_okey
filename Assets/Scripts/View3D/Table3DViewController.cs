using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.View3D
{
    /// <summary>
    /// Ada 101 Okey tarzı 3D Masa Ortamını, sol/sağ 3D yan ıstakaları,
    /// 3D deste yığınını, göstergeyi ve ortada 3D açılan perleri yönetir.
    /// </summary>
    public class Table3DViewController : MonoBehaviour
    {
        [Header("3D Masa & Deste Transformları")]
        [SerializeField] private Transform drawDeckAnchor;
        [SerializeField] private Transform indicatorTileAnchor;
        [SerializeField] private Tile3DView indicatorTile3DView;
        [SerializeField] private TextMeshPro remainingDeckCount3DText;

        [Header("4 Oyuncunun 3D Taş Atma Bölgeleri (Discards)")]
        [SerializeField] private Transform[] playerDiscardAnchors; // 0: Sağınız, 1: Üst, 2: Sol, 3: Yandan Al

        [Header("Masadaki 3D Açık Perler")]
        [SerializeField] private Transform tableMeldsContainer;
        [SerializeField] private GameObject meldGroupPrefab;
        [SerializeField] private GameObject tile3DPrefab;

        [Header("3D Yan Ahşap Istakalar (Opponent 3D Racks)")]
        [SerializeField] private Transform leftOpponentRack3D;
        [SerializeField] private Transform rightOpponentRack3D;
        [SerializeField] private Transform topOpponentRack3D;

        public event Action OnDrawDeckClicked;
        public event Action<int> OnDiscardZoneClicked;

        public void SetIndicator(TileData indicatorTile, int remainingDeckCount)
        {
            if (indicatorTile3DView != null)
            {
                indicatorTile3DView.BindData(indicatorTile);
            }

            UpdateDeckCount(remainingDeckCount);
        }

        public void UpdateDeckCount(int count)
        {
            if (remainingDeckCount3DText != null)
            {
                remainingDeckCount3DText.text = count.ToString();
            }
        }

        public void AddDiscardTile(int playerIndex, TileData tile)
        {
            if (playerIndex < 0 || playerIndex >= playerDiscardAnchors.Length || tile3DPrefab == null) return;

            var container = playerDiscardAnchors[playerIndex];
            var tileObj = Instantiate(tile3DPrefab, container);
            var view = tileObj.GetComponent<Tile3DView>();
            view.BindData(tile);
        }

        public void ClearTable()
        {
            if (tableMeldsContainer != null)
            {
                foreach (Transform child in tableMeldsContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public void RenderOpenedPer(PerGroup perGroup)
        {
            if (tableMeldsContainer == null || meldGroupPrefab == null || tile3DPrefab == null) return;

            var groupObj = Instantiate(meldGroupPrefab, tableMeldsContainer);
            foreach (var tile in perGroup.Tiles)
            {
                var tileObj = Instantiate(tile3DPrefab, groupObj.transform);
                var view = tileObj.GetComponent<Tile3DView>();
                view.BindData(tile);
            }
        }

        public void RemoveLastDiscardTile(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerDiscardAnchors.Length) return;
            var container = playerDiscardAnchors[playerIndex];
            if (container != null && container.childCount > 0)
            {
                Destroy(container.GetChild(container.childCount - 1).gameObject);
            }
        }

        public void RefreshAllTableMelds(IEnumerable<PerGroup> seriesMelds, IEnumerable<PerGroup> pairMelds)
        {
            ClearTable();
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
    }
}
