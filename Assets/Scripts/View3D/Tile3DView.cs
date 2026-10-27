using System;
using UnityEngine;
using TMPro;
using Okey101.Core.Data;
using Okey101.Core.Enums;

namespace Okey101.View3D
{
    /// <summary>
    /// 3D Masa ortamında tek bir taşın fiziksel 3D mesh modelini,
    /// kalınlığını, yüzey kaplamasını, sayı/renk materyalini ve seçim animasyonunu yönetir.
    /// </summary>
    public class Tile3DView : MonoBehaviour
    {
        [Header("3D Mesh ve Görsel Referanslar")]
        [SerializeField] private MeshRenderer tileMeshRenderer;
        [SerializeField] private TextMeshPro numberTextMesh;
        [SerializeField] private SpriteRenderer suitIconRenderer;
        [SerializeField] private GameObject okeyStarObject;
        [SerializeField] private GameObject fakeOkeyBadgeObject;

        [Header("3D Materyaller & Renkler")]
        [SerializeField] private Material standardTileMaterial;
        [SerializeField] private Material selectedHighlightMaterial;
        [SerializeField] private Material okeyFlippedMaterial;

        [SerializeField] private Color redColor = new Color(0.85f, 0.15f, 0.15f);
        [SerializeField] private Color yellowColor = new Color(0.95f, 0.72f, 0.05f);
        [SerializeField] private Color blueColor = new Color(0.12f, 0.45f, 0.9f);
        [SerializeField] private Color blackColor = new Color(0.15f, 0.15f, 0.15f);

        public TileData Data { get; private set; }
        public bool IsSelected { get; private set; }
        public int SlotIndex { get; set; } = -1;

        private Vector3 _originalLocalPos;
        private Quaternion _originalLocalRot;

        public event Action<Tile3DView> OnTileClicked;

        private void Awake()
        {
            _originalLocalPos = transform.localPosition;
            _originalLocalRot = transform.localRotation;
        }

        public void BindData(TileData tileData, bool isFlippedOkeyInRack = false)
        {
            Data = tileData;
            IsSelected = false;

            if (tileData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            Color color = GetColorForTile(tileData.Color);

            if (numberTextMesh != null)
            {
                numberTextMesh.text = tileData.Number > 0 ? tileData.Number.ToString() : string.Empty;
                numberTextMesh.color = color;
            }

            if (suitIconRenderer != null)
            {
                suitIconRenderer.color = color;
            }

            // Okey Yıldızı / Ters Çevrilmiş Okey Durumu
            if (tileData.IsOkey)
            {
                if (okeyStarObject != null) okeyStarObject.SetActive(true);
                if (isFlippedOkeyInRack)
                {
                    // Istakada ters durur (180 derece ters dönüş)
                    transform.localRotation = _originalLocalRot * Quaternion.Euler(0, 0, 180f);
                }
                else
                {
                    transform.localRotation = _originalLocalRot;
                }
            }
            else
            {
                if (okeyStarObject != null) okeyStarObject.SetActive(false);
                transform.localRotation = _originalLocalRot;
            }

            // Sahte Okey Rozeti
            if (fakeOkeyBadgeObject != null)
            {
                fakeOkeyBadgeObject.SetActive(tileData.IsFakeOkey);
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            // 3D Istakada seçilen taş yukarı doğru 0.08 birim yükselir
            transform.localPosition = IsSelected ? _originalLocalPos + new Vector3(0, 0.08f, 0) : _originalLocalPos;
        }

        public void ToggleSelected()
        {
            SetSelected(!IsSelected);
        }

        private void OnMouseDown()
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
                _ => Color.white
            };
        }
    }
}
