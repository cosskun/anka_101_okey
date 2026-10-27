using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Okey101.Core.Data;

namespace Okey101.View
{
    /// <summary>
    /// Unity UI ve 2D/3D ortamında taşların fare / dokunmatik ile sürükle-bırak (Drag & Drop)
    /// etkileşimini yöneten bileşen.
    /// Istaka içinde slot değiştirme, masaya yere taş atma ve açık perlere taş işleme desteği sağlar.
    /// </summary>
    public class TileDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Canvas _parentCanvas;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalPosition;
        private Vector3 _originalScale;
        private Transform _originalParent;

        public TileView TileView { get; private set; }

        public static event Action<TileView, Vector2> OnTileDroppedOnTable;
        public static event Action<TileView, int, int> OnTileDroppedOnRackSlot; // (tile, targetRow, targetCol)
        public static event Action<TileView> OnTileDroppedOnDiscardZone;

        private void Awake()
        {
            TileView = GetComponent<TileView>();
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _parentCanvas = GetComponentInParent<Canvas>();
            _originalScale = transform.localScale;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (TileView == null || TileView.Data == null) return;

            _originalPosition = _rectTransform.position;
            _originalScale = transform.localScale;
            _originalParent = transform.parent;

            // Mobil dokunmatik ve masaüstü sürüklemede taşın parmak altında kalmaması için hafif büyüt ve yarı saydam yap
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.88f;
            transform.localScale = _originalScale * 1.08f;

            if (_parentCanvas != null)
            {
                transform.SetParent(_parentCanvas.transform, true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (TileView == null || TileView.Data == null) return;

            if (_parentCanvas != null && _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rectTransform.position = eventData.position;
            }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _rectTransform, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out Vector3 worldPoint);
                _rectTransform.position = worldPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1.0f;
            transform.localScale = _originalScale;

            // Eğer geçerli bir drop bölgesine bırakılmadıysa orijinal yerine geri dön
            transform.SetParent(_originalParent, true);
            _rectTransform.position = _originalPosition;

            if (EventSystem.current == null) return;

            // Raycast kontrolü ile bırakılan hedefi tespit et
            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);

            foreach (var result in raycastResults)
            {
                if (result.gameObject == gameObject) continue;

                // 1. Yere Taş Atma Bölgesi mi?
                if (result.gameObject.CompareTag("DiscardZone"))
                {
                    OnTileDroppedOnDiscardZone?.Invoke(TileView);
                    return;
                }

                // 2. Istakadaki başka bir slot veya taş üzeri mi?
                var targetSlot = result.gameObject.GetComponentInParent<RackSlotView>();
                if (targetSlot != null)
                {
                    OnTileDroppedOnRackSlot?.Invoke(TileView, targetSlot.Row, targetSlot.Col);
                    return;
                }

                var otherTileView = result.gameObject.GetComponentInParent<TileView>();
                if (otherTileView != null && otherTileView != TileView && otherTileView.SlotRow >= 0 && otherTileView.SlotCol >= 0)
                {
                    OnTileDroppedOnRackSlot?.Invoke(TileView, otherTileView.SlotRow, otherTileView.SlotCol);
                    return;
                }

                // 3. Masadaki Açık Per Bölgesi mi?
                if (result.gameObject.CompareTag("TableCenterZone"))
                {
                    OnTileDroppedOnTable?.Invoke(TileView, eventData.position);
                    return;
                }
            }
        }
    }
}
