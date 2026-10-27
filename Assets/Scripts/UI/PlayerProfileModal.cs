using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Okey101.UI
{
    /// <summary>
    /// Ada 101 Okey tarzı detaylı Oyuncu Profil İnceleme Penceresi (Modal).
    /// Kullanıcı veya botların avatarına tıklandığında istatistiklerini, çiplerini,
    /// klanını, madalyalarını ve fotoğraflarını görüntüler.
    /// </summary>
    public class PlayerProfileModal : MonoBehaviour
    {
        [Header("Modal Panel")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;

        [Header("Profil Bilgileri")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI playerIdText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI clanNameText;
        [SerializeField] private TextMeshProUGUI coinCountText;
        [SerializeField] private TextMeshProUGUI charmPointsText;

        [Header("İstatistikler")]
        [SerializeField] private TextMeshProUGUI gamesPlayedText;
        [SerializeField] private TextMeshProUGUI winRateText;
        [SerializeField] private TextMeshProUGUI followersCountText;

        [Header("Gönderi & Fotoğraf")]
        [SerializeField] private Image postPhotoImage;
        [SerializeField] private TextMeshProUGUI likesCountText;
        [SerializeField] private TextMeshProUGUI commentsCountText;

        public class ProfileData
        {
            public string Id;
            public string Name;
            public string Clan;
            public string Coins;
            public int Charm;
            public int GamesPlayed;
            public string WinRate;
            public int Followers;
            public Sprite AvatarSprite;
            public Sprite PostSprite;
            public int Likes;
            public int Comments;
        }

        private void Awake()
        {
            closeButton?.onClick.AddListener(Hide);
        }

        public void ShowProfile(ProfileData data)
        {
            if (data == null || modalRoot == null) return;

            modalRoot.SetActive(true);

            if (playerIdText != null) playerIdText.text = $"ID: {data.Id}";
            if (playerNameText != null) playerNameText.text = data.Name;
            if (clanNameText != null) clanNameText.text = data.Clan;
            if (coinCountText != null) coinCountText.text = data.Coins;
            if (charmPointsText != null) charmPointsText.text = data.Charm.ToString();

            if (gamesPlayedText != null) gamesPlayedText.text = data.GamesPlayed.ToString();
            if (winRateText != null) winRateText.text = data.WinRate;
            if (followersCountText != null) followersCountText.text = data.Followers.ToString();

            if (likesCountText != null) likesCountText.text = data.Likes.ToString();
            if (commentsCountText != null) commentsCountText.text = data.Comments.ToString();

            if (avatarImage != null && data.AvatarSprite != null) avatarImage.sprite = data.AvatarSprite;
            if (postPhotoImage != null && data.PostSprite != null) postPhotoImage.sprite = data.PostSprite;
        }

        public void Hide()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
        }
    }
}
