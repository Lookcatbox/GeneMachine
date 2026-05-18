using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
[RequireComponent(typeof(VideoPlayer))]
public class VideoPlaylistPlayer : MonoBehaviour
{
    [System.Serializable]
    public class PlaylistItem
    {
        public string title;
        public VideoClip clip;
        public string url;
    }

    [Header("Playback")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private List<PlaylistItem> playlist = new List<PlaylistItem>();
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loopPlaylist = true;
    [SerializeField] private int startIndex = 0;

    private int currentIndex = -1;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += HandleVideoEnded;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayAt(startIndex);
        }
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoEnded;
        }
    }

    public void PlayAt(int index)
    {
        if (!IsValidIndex(index))
        {
            return;
        }

        currentIndex = index;
        PlayCurrent();
    }

    public void PlayNext()
    {
        if (playlist.Count == 0)
        {
            return;
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= playlist.Count)
        {
            if (!loopPlaylist)
            {
                currentIndex = -1;
                return;
            }

            nextIndex = 0;
        }

        currentIndex = nextIndex;
        PlayCurrent();
    }

    public void PlayPrevious()
    {
        if (playlist.Count == 0)
        {
            return;
        }

        int previousIndex = currentIndex - 1;
        if (previousIndex < 0)
        {
            if (!loopPlaylist)
            {
                return;
            }

            previousIndex = playlist.Count - 1;
        }

        currentIndex = previousIndex;
        PlayCurrent();
    }

    public void RefreshCurrent()
    {
        PlayCurrent();
    }

    private void HandleVideoEnded(VideoPlayer source)
    {
        if (source != videoPlayer)
        {
            return;
        }

        PlayNext();
    }

    private void PlayCurrent()
    {
        if (!IsValidIndex(currentIndex) || videoPlayer == null)
        {
            return;
        }

        PlaylistItem item = playlist[currentIndex];
        videoPlayer.Stop();

        if (item.clip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = item.clip;
            videoPlayer.url = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(item.url))
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.clip = null;
            videoPlayer.url = item.url;
        }
        else
        {
            return;
        }

        videoPlayer.Play();
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < playlist.Count;
    }
}