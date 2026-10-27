using System;
using UnityEngine;
using UnityEngine.UI;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.View
{
    /// <summary>
    /// Masada veya ıstakada gösterilen tek bir taşın 3D / 2D görsel temsili ve etkileşim bileşeni.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [Header("UI / Text Referansları")]
        [SerializeField] private Text numberText;
        [SerializeField] private Image tileColorIcon;
        [SerializeField] private GameObject fakeOkeyBadge;
        [SerializeField] private GameObject okeyJokerStar;
        [SerializeField] private GameObject selectionHighlight;

        [Header("Renk Paleti")]
        [SerializeField] private Color redColor = new Color(0.85f, 0.15f, 0.15f);
        [SerializeField] private Color yellowColor = new Color(0.95f, 0.75f, 0.1f);
        [SerializeField] private Color blueColor = new Color(0.15f, 0.45f, 0.9f);
        [SerializeField] private Color blackColor = new Color(0.15f, 0.15f, 0.15f);

        public TileData Data { get; private set; }
        public bool IsSelected { get; private set; }
        public int SlotRow { get; set; } = -1;
        public int SlotCol { get; set; } = -1;

        public event Action<TileView> OnTileClicked;

        /// <summary>
        /// Taş verisini görünüme bağlar ve sayı/renk etiketlerini günceller.
        /// </summary>
        public void BindData(TileData tileData)
        {
            Data = tileData;
            IsSelected = false;
            UpdateSelectionVisual();

            if (tileData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Renk ve Sayı
            Color visualColor = GetColorForTile(tileData.Color);
            if (numberText != null)
            {
                numberText.text = tileData.Number > 0 ? tileData.Number.ToString() : string.Empty;
                numberText.color = visualColor;
            }

            if (tileColorIcon != null)
            {
                tileColorIcon.color = visualColor;
            }

            // Sahte Okey Rozeti
            if (fakeOkeyBadge != null)
            {
                fakeOkeyBadge.SetActive(tileData.IsFakeOkey);
            }

            // Okey Yıldızı / Joker İkonu
            if (okeyJokerStar != null)
            {
                okeyJokerStar.SetActive(tileData.IsOkey);
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            UpdateSelectionVisual();
        }

        public void ToggleSelected()
        {
            SetSelected(!IsSelected);
        }

        private void UpdateSelectionVisual()
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(IsSelected);
            }
        }

        private void OnMouseDown()
        {
            OnTileClicked?.Invoke(this);
        }

        public void OnClickFromUI()
        {
            OnTileClicked?.Invoke(this);
        }

        private Color GetColorForTile(TileColor color)
        {
            return color switch
            {
                TileColor.Red => redColor,
                TileColor.Yellow => yellowColor,
                TileColor.Blue => blueColor,
                TileColor.Black => blackColor,
                _ => Color.gray
            };
        }
    }
}
