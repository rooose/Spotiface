using System;
using System.Threading.Tasks;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System.Linq;
using ZeroMQ;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SpotifyAPI.Web.Examples.CLI
{
    internal static class Program
    {
        private static string _clientId = ""; //"";
        private static string _secretId = ""; //"";
        private  const int CHANGE_SONG_BUFFER_MS = 1000;

        private static SpotifyWebAPI _spotify;
        private static readonly Dictionary<string,string> _playlistMap = new Dictionary<string, string>()
        {
            {"happy",       "spotify:playlist:70x6C39NKrjYZryhRkuxfJ"},
            {"sad",         "spotify:playlist:5Ty5Wicbp9UOjBxdylJ3yA"},
            {"neutral",     "spotify:playlist:4bXVAmbOjChI697fuBMPLY"},
            {"angry",       "spotify:playlist:2H7ZU6MppCCjc0Qm3arYOy"},
            {"disgust",     "spotify:playlist:3KpNiPpOXiq7hr5vjw57mg"},
            {"fear",        "spotify:playlist:41LWUS2eOZeyjd0aWK55mX"},
            {"surprise",    "spotify:playlist:3wSHHOPz9NXzfsfAjq3RL8"}
        };

        // ReSharper disable once UnusedParameter.Local
        public static void Main(string[] args)
        {
            _clientId = string.IsNullOrEmpty(_clientId) ?
             Environment.GetEnvironmentVariable("SPOTIFACE_CLIENT_ID") :
             _clientId;

            _secretId = string.IsNullOrEmpty(_secretId) ?
              Environment.GetEnvironmentVariable("SPOTIFACE_SECRET") :
              _secretId;

            var auth =
              new AuthorizationCodeAuth(_clientId, _secretId, "http://localhost:4002", "http://localhost:4002",
                Scope.PlaylistReadPrivate | Scope.PlaylistReadCollaborative | Scope.UserReadPlaybackState | Scope.UserModifyPlaybackState);
            auth.AuthReceived += AuthOnAuthReceived;
            auth.Start();
            auth.OpenBrowser();

            using (var context = new ZContext())
            using (var socket = new ZSocket(context, ZSocketType.REP))
            {
                var emotionStack = new List<string>();
                var lastMostCommon = "";

                socket.Bind("tcp://127.0.0.1:5555");
                LaunchEmotionDetection(Path.GetFullPath(@"..\..\..\..\..\src\") , "video_emotion_color_demo.py");

                while (true)
                {
                    using (ZFrame reply = socket.ReceiveFrame())
                    {
                        emotionStack.Add(reply.ReadString());
                        Console.WriteLine("RECIEVED {0}", emotionStack[emotionStack.Count - 1]);
                    }
                    socket.Send(new ZFrame("Thanks"));

                    PlaybackContext playbackContext = _spotify.GetPlayback();
                    var isReadyForNewSong = !playbackContext.IsPlaying || playbackContext.ProgressMs >= (playbackContext.Item.DurationMs - CHANGE_SONG_BUFFER_MS);
                    Console.WriteLine("{0} ms before next song!", playbackContext.Item.DurationMs - CHANGE_SONG_BUFFER_MS - playbackContext.ProgressMs);
                    if (isReadyForNewSong) {
                        Console.WriteLine("READY FOR A NEW SONG");
                        string mostCommon = emotionStack.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
                        if (mostCommon != lastMostCommon)
                        {
                            Console.WriteLine("Most common since last time was {0}", mostCommon);
                            lastMostCommon = mostCommon;
                            _spotify.ResumePlayback(contextUri: _playlistMap[mostCommon], offset: 0, positionMs: 0);
                            _spotify.SetShuffle(true);
                            emotionStack.Clear();
                        }
                    }
                }
            }
        }

        private static async void AuthOnAuthReceived(object sender, AuthorizationCode payload)
        {
            var auth = (AuthorizationCodeAuth)sender;
            auth.Stop();

            Token token = await auth.ExchangeCode(payload.Code);
            _spotify = new SpotifyWebAPI
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            await PrintUsefulData(_spotify);
        }

        private static async Task PrintAllPlaylistTracks(SpotifyWebAPI api, Paging<SimplePlaylist> playlists)
        {
            if (playlists.Items == null) return;

            playlists.Items.ForEach(playlist => Console.WriteLine($"- {playlist.Name}"));
            if (playlists.HasNextPage())
                await PrintAllPlaylistTracks(api, await api.GetNextPageAsync(playlists));
        }

        private static async Task PrintUsefulData(SpotifyWebAPI api)
        {
            PrivateProfile profile = await api.GetPrivateProfileAsync();
            string name = string.IsNullOrEmpty(profile.DisplayName) ? profile.Id : profile.DisplayName;
            Console.WriteLine($"Hello there, {name}!");

            Console.WriteLine("Your playlists:");
            await PrintAllPlaylistTracks(api, api.GetUserPlaylists(profile.Id));
        }

        private static void LaunchEmotionDetection(string cwd, string cmd)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = @"C:\Users\Rose Hirigoyen\AppData\Local\Programs\Python\Python37\python.exe",
                Arguments = string.Format("{0}", cmd),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = cwd
            };
            var proc = Process.Start(start);
        }
    }
}
