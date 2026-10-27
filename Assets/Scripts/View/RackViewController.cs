using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Okey101.Core.Data;

namespace Okey101.View
{
    /// <summary>
    /// Çift sıralı ıstaka görünümünü (2 sıra x 13 sütun) ve oyuncunun taş seçimlerini yönetir.
    /// </summary>
    public class RackViewController : MonoBehaviour
    {
        [Header("Prefab & Slot Ayarları")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Transform[] rowContainers; // [0] = Üst Sıra, [1] = Alt Sıra
        [SerializeField] private int columns = 13;

        private TileView[,] _slotViews;
        private PlayerHand _currentHand;

        public event Action<List<TileData>> OnSelectionChanged;
        public event Action<TileData> OnTileDoubleClicked;

        private void Awake()
        {
            _slotViews = new TileView[2, columns];
            InitializeSlots();
        }

        private void OnEnable()
        {
            TileDragHandler.OnTileDroppedOnRackSlot += HandleTileDroppedOnRackSlot;
        }

        private void OnDisable()
        {
            TileDragHandler.OnTileDroppedOnRackSlot -= HandleTileDroppedOnRackSlot;
        }

        private void HandleTileDroppedOnRackSlot(TileView draggedView, int targetRow, int targetCol)
        {
            if (_currentHand == null || draggedView == null || draggedView.Data == null) return;
            MoveOrSwapRackSlot(draggedView.SlotRow, draggedView.SlotCol, targetRow, targetCol);
        }

        /// <summary>
        /// Istaka içinde sürükle-bırak ile iki slotun taşlarını yer değiştirir veya boş slota taşır.
        /// </summary>
        public void MoveOrSwapRackSlot(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (_currentHand == null) return;
            if (fromRow < 0 || fromRow >= 2 || fromCol < 0 || fromCol >= columns) return;
            if (toRow < 0 || toRow >= 2 || toCol < 0 || toCol >= columns) return;
            if (fromRow == toRow && fromCol == toCol) return;

            var movingTile = _currentHand.RackGrid[fromRow, fromCol];
            var targetTile = _currentHand.RackGrid[toRow, toCol];

            _currentHand.RackGrid[toRow, toCol] = movingTile;
            _currentHand.RackGrid[fromRow, fromCol] = targetTile;

            RefreshRack();
        }

        private void InitializeSlots()
        {
            if (rowContainers == null || rowContainers.Length < 2 || tilePrefab == null) return;

            for (int r = 0; r < 2; r++)
            {
                var container = rowContainers[r];
                for (int c = 0; c < columns; c++)
                {
                    var tileObj = Instantiate(tilePrefab, container);
                    var view = tileObj.GetComponent<TileView>();
                    view.SlotRow = r;
                    view.SlotCol = c;
                    view.OnTileClicked += HandleTileClicked;
                    view.BindData(null);
                    _slotViews[r, c] = view;
                }
            }
        }

        public void BindHand(PlayerHand hand)
        {
            _currentHand = hand;
            RefreshRack();
        }

        public void RefreshRack()
        {
            if (_currentHand == null) return;

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var tileData = _currentHand.RackGrid[r, c];
                    if (_slotViews[r, c] != null)
                    {
                        _slotViews[r, c].BindData(tileData);
                    }
                }
            }

            NotifySelection();
        }

        private void HandleTileClicked(TileView clickedView)
        {
            if (clickedView.Data == null) return;

            clickedView.ToggleSelected();
            NotifySelection();
        }

        public List<TileData> GetSelectedTiles()
        {
            var selected = new List<TileData>();
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var view = _slotViews[r, c];
                    if (view != null && view.IsSelected && view.Data != null)
                    {
                        selected.Add(view.Data);
                    }
                }
            }
            return selected;
        }

        public void ClearSelection()
        {
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (_slotViews[r, c] != null)
                    {
                        _slotViews[r, c].SetSelected(false);
                    }
                }
            }
            NotifySelection();
        }

        private void NotifySelection()
        {
            OnSelectionChanged?.Invoke(GetSelectedTiles());
        }

        /// <summary>
        /// Istakadaki taşları renge ve sayıya göre sıralar.
        /// </summary>
        public void OnSortRunsClicked()
        {
            if (_currentHand == null) return;
            _currentHand.SortByColorAndNumber();
            ClearSelection();
            RefreshRack();
        }

        /// <summary>
        /// Istakadaki taşları çift oluşturacak şekilde sıralar.
        /// </summary>
        public void OnSortPairsClicked()
        {
            if (_currentHand == null) return;
            _currentHand.SortByPairs();
            ClearSelection();
            RefreshRack();
        }
    }
}
