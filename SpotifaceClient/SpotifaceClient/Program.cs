using System;
using System.Threading.Tasks;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System.Linq;
using System.Text;
using System.Threading;
using ZeroMQ;
using System.Collections.Generic;

namespace SpotifyAPI.Web.Examples.CLI
{
    internal static class Program
    {
        private static string _clientId = ""; //"";
        private static string _secretId = ""; //"";

        private static Dictionary<string,string> _playlistMap = new Dictionary<string, string>()
        {
            {"happy","70x6C39NKrjYZryhRkuxfJ"},
            {"sad", "5Ty5Wicbp9UOjBxdylJ3yA"},
            {"neutral", "4bXVAmbOjChI697fuBMPLY"},
            {"angry", "2H7ZU6MppCCjc0Qm3arYOy"},
            {"disgust", "3KpNiPpOXiq7hr5vjw57mg"},
            {"fear", "41LWUS2eOZeyjd0aWK55mX"},
            {"surprise", "3wSHHOPz9NXzfsfAjq3RL8"}
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
                Scope.PlaylistReadPrivate | Scope.PlaylistReadCollaborative);
            auth.AuthReceived += AuthOnAuthReceived;
            auth.Start();
            auth.OpenBrowser();

            using (var context = new ZContext())
            using (var socket = new ZSocket(context, ZSocketType.REP))
            {
                socket.Bind("tcp://127.0.0.1:5555");

                while(true)
                {
                    Console.Write("waiting");
                    using (ZFrame reply = socket.ReceiveFrame())
                    {
                        String msg = reply.ReadString();
                        Console.WriteLine("Hello {0}!", msg);
                    }

                    socket.Send(new ZFrame("Thanks"));

                }
            }

            auth.Stop(0);

        }

        private static async void AuthOnAuthReceived(object sender, AuthorizationCode payload)
        {
            var auth = (AuthorizationCodeAuth)sender;
            auth.Stop();

            Token token = await auth.ExchangeCode(payload.Code);
            var api = new SpotifyWebAPI
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };
            await PrintUsefulData(api);
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

        private static List<SpotifyAPI.Web.Models.PlaylistTrack> GetPlaylistSongs(SpotifyWebAPI api, String playlistID)
        {
            Paging<PlaylistTrack> playlist = api.GetPlaylistTracks(playlistID);
            playlist.Items.ForEach(track => Console.WriteLine(track.Track.Name));
            return playlist.Items;
        }
    }
}
