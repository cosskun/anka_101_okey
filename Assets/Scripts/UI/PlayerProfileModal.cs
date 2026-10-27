using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Okey101.UI
{
    /// <summary>
    /// Çevrimdışı (Offline) Yerel Oyuncu ve Yapay Zeka Bot Profil Penceresi.
    /// </summary>
    public class PlayerProfileModal : MonoBehaviour
    {
        [Header("Modal Panel")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;

        [Header("Profil Bilgileri")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI roleLabelText;
        [SerializeField] private TextMeshProUGUI coinCountText;

        [Header("Yerel Oyun İstatistikleri")]
        [SerializeField] private TextMeshProUGUI gamesPlayedText;
        [SerializeField] private TextMeshProUGUI gamesWonText;
        [SerializeField] private TextMeshProUGUI winRateText;

        public class ProfileData
        {
            public string Name;
            public bool IsBot;
            public string Coins;
            public int GamesPlayed;
            public int GamesWon;
            public string WinRate;
            public Sprite AvatarSprite;
        }

        private void Awake()
        {
            closeButton?.onClick.AddListener(Hide);
        }

        public void ShowProfile(ProfileData data)
        {
            if (data == null || modalRoot == null) return;

            modalRoot.SetActive(true);

            if (playerNameText != null) playerNameText.text = data.Name;
            if (roleLabelText != null) roleLabelText.text = data.IsBot ? "Yapay Zeka Bot Rakip" : "Yerel Oyuncu Profili";
            if (coinCountText != null) coinCountText.text = data.Coins;

            if (gamesPlayedText != null) gamesPlayedText.text = data.GamesPlayed.ToString();
            if (gamesWonText != null) gamesWonText.text = data.GamesWon.ToString();
            if (winRateText != null) winRateText.text = data.WinRate;

            if (avatarImage != null && data.AvatarSprite != null) avatarImage.sprite = data.AvatarSprite;
        }

        public void Hide()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
        }
    }
}
