using System;
using System.Collections.Generic;
using UnityEngine;
using Okey101.Core.Data;

namespace Okey101.View3D
{
    /// <summary>
    /// 3D Çift sıralı ahşap ıstakayı ve üzerindeki 2x13 fiziksel 3D slotları yönetir.
    /// Taşların 3D yerleşimini, farenin raycast seçimini ve sürükle-bırak animasyonlarını sağlar.
    /// </summary>
    public class Rack3DViewController : MonoBehaviour
    {
        [Header("3D Slot Transformları (2x13 = 26 Slot)")]
        [SerializeField] private Transform[] topRowSlotAnchors;    // 13 Adet Üst Sıra
        [SerializeField] private Transform[] bottomRowSlotAnchors; // 13 Adet Alt Sıra
        [SerializeField] private GameObject tile3DPrefab;

        private Tile3DView[] _spawnedTileViews = new Tile3DView[26];
        private PlayerHand _currentHand;

        public event Action<List<TileData>> OnSelectionChanged;

        private void Awake()
        {
            Initialize3DRackSlots();
        }

        private void Initialize3DRackSlots()
        {
            if (tile3DPrefab == null) return;

            for (int i = 0; i < 26; i++)
            {
                Transform parentAnchor = (i < 13) 
                    ? topRowSlotAnchors[i] 
                    : bottomRowSlotAnchors[i - 13];

                if (parentAnchor != null)
                {
                    var tileObj = Instantiate(tile3DPrefab, parentAnchor);
                    var view = tileObj.GetComponent<Tile3DView>();
                    view.SlotIndex = i;
                    view.OnTileClicked += HandleTileClicked;
                    view.BindData(null);
                    _spawnedTileViews[i] = view;
                }
            }
        }

        public void BindHand(PlayerHand hand)
        {
            _currentHand = hand;
            Refresh3DRack();
        }

        public void Refresh3DRack()
        {
            if (_currentHand == null) return;

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 13; c++)
                {
                    int index = r * 13 + c;
                    var tileData = _currentHand.RackGrid[r, c];
                    if (_spawnedTileViews[index] != null)
                    {
                        // Okey taşı ıstakada ters durur
                        _spawnedTileViews[index].BindData(tileData, isFlippedOkeyInRack: true);
                    }
                }
            }
        }

        private void HandleTileClicked(Tile3DView clickedView)
        {
            if (clickedView.Data == null) return;
            clickedView.ToggleSelected();
            NotifySelection();
        }

        public List<TileData> GetSelectedTiles()
        {
            var list = new List<TileData>();
            foreach (var view in _spawnedTileViews)
            {
                if (view != null && view.IsSelected && view.Data != null)
                {
                    list.Add(view.Data);
                }
            }
            return list;
        }

        public void ClearSelection()
        {
            foreach (var view in _spawnedTileViews)
            {
                if (view != null) view.SetSelected(false);
            }
            NotifySelection();
        }

        private void NotifySelection()
        {
            OnSelectionChanged?.Invoke(GetSelectedTiles());
        }

        public void OnSortRunsClicked()
        {
            if (_currentHand == null) return;
            _currentHand.SortByColorAndNumber();
            ClearSelection();
            Refresh3DRack();
        }

        public void OnSortPairsClicked()
        {
            if (_currentHand == null) return;
            _currentHand.SortByPairs();
            ClearSelection();
            Refresh3DRack();
        }

        /// <summary>
        /// 3D Istakada iki slotun taşlarını yer değiştirir veya boş slota taşır.
        /// </summary>
        public void MoveOrSwap3DRackSlot(int fromSlotIndex, int toSlotIndex)
        {
            if (_currentHand == null) return;
            if (fromSlotIndex < 0 || fromSlotIndex >= 26 || toSlotIndex < 0 || toSlotIndex >= 26) return;
            if (fromSlotIndex == toSlotIndex) return;

            int fromRow = fromSlotIndex / 13;
            int fromCol = fromSlotIndex % 13;
            int toRow = toSlotIndex / 13;
            int toCol = toSlotIndex % 13;

            var movingTile = _currentHand.RackGrid[fromRow, fromCol];
            var targetTile = _currentHand.RackGrid[toRow, toCol];

            _currentHand.RackGrid[toRow, toCol] = movingTile;
            _currentHand.RackGrid[fromRow, fromCol] = targetTile;

            Refresh3DRack();
        }
    }
}
