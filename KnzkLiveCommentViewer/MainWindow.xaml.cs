using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Http;
using WebSocketSharp;

namespace KnzkLiveCommentViewer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public System.Collections.ObjectModel.ObservableCollection<CommentRecord> Comments;
        private BouyomiConnection bouyomichan;
        public KnzkLiveLiveInformation liveInfo;

        public MainWindow()
        {
            InitializeComponent();
            this.Comments = new System.Collections.ObjectModel.ObservableCollection<CommentRecord>();
            this.LogListView.DataContext = this.Comments;
            this.bouyomichan = new BouyomiConnection();
        }

        private void AddComment(CommentRecord comment)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (comment.CreatedAt == null || comment.CreatedAt.Ticks == 0)
                {
                    comment.CreatedAt = new DateTime(DateTime.Now.Ticks);
                }
                this.Comments.Insert(0, comment);
            });
        }

        private async void WatchStateButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("clicked");

            var watchPageUrl = ((MainWindowModel)this.DataContext).WatchPageUrl;
            var regex = Regex.Match(watchPageUrl, "^https://live\\.knzk\\.me/watch([0-9]+)$");
            if (regex.Success == false)
            {
                this.AddComment(new CommentRecord
                {
                    Type = "Sys",
                    UserName = "Error",
                    Content = "URLがおかしいです",
                });
                return;
            }
            var id = int.Parse(regex.Groups[1].Value);
            this.AddComment(new CommentRecord {
                Type = "Sys",
                UserName = "Info",
                Content = "ID(=" + id + ")認識、配信情報を取得します…",
            });
            Console.WriteLine(id);

            using (var client = new HttpClient())
            {
                var result = await client.GetAsync("https://live.knzk.me/api/client/watch?id=" + id);
                Console.WriteLine(result.ToString());
                if (!result.IsSuccessStatusCode)
                {
                    this.AddComment(new CommentRecord
                    {
                        Type = "Sys",
                        UserName = "Error",
                        Content = "配信情報の取得に失敗(HTTP_STATUS=" + result.StatusCode + ")",
                    });
                    return;
                }
                using (var stream = await result.Content.ReadAsStreamAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(KnzkLiveLiveInformation));
                    this.liveInfo =(KnzkLiveLiveInformation) serializer.ReadObject(stream);
                }
            }
            this.AddComment(new CommentRecord
            {
                Type = "Sys",
                UserName = "Info",
                Content = "配信情報の取得に成功、ハッシュタグ #" + this.liveInfo.Hashtag,
            });

            var ws = new WebSocket("wss://knzk.me/api/v1/streaming?stream=hashtag&tag=" + System.Uri.EscapeDataString(liveInfo.Hashtag));
            ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            
            ws.OnOpen += (_s, _e) =>
            {
                this.AddComment(new CommentRecord {
                    Type = "Sys",
                    UserName = "Info",
                    Content = "Mastodonのストリームに接続しました",
                });
            };

            ws.OnMessage += (_s, _e) =>
            {
                Console.WriteLine("event" + _e.Data);
                var dataStream = new MemoryStream(Encoding.UTF8.GetBytes(_e.Data));
                var eventContainerSerializer = new DataContractJsonSerializer(typeof(MastodonStreamingEvent));
                var streamEvent = (MastodonStreamingEvent)eventContainerSerializer.ReadObject(dataStream);
                if (streamEvent.EventType != "update")
                {
                    Console.WriteLine("eventtype is not update: " + streamEvent.EventType);
                    return;
                }
                dataStream.Close();
                dataStream = new MemoryStream(Encoding.UTF8.GetBytes(streamEvent.Payload));
                var statusSerializer = new DataContractJsonSerializer(typeof(MastodonStatus));
                var status = (MastodonStatus)statusSerializer.ReadObject(dataStream);
                var record = new CommentRecord
                {
                    Type = "丼",
                    UserName = status.Account.Name,
                    Content = Regex.Replace(status.Content, "<.+?>", "").Replace("#" + this.liveInfo.Hashtag, ""),
                    CreatedAt = DateTime.Parse(status.CreatedAt),
                };
                this.bouyomichan.say(record.Content);
                this.AddComment(record);
            };
            ws.Connect();
        }

        private void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            throw new NotImplementedException();
        }
    }

    public class MainWindowModel
    {
        public string WatchPageUrl { get; set; }
    }

    public class CommentRecord {
        public string Type { get; set; }
        public string UserName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtString
        {
            get { return this.CreatedAt.ToString(); }
        }
    }

    [DataContract]
    public class MastodonStreamingEvent {
        [DataMember(Name="event")]
        public string EventType { get; set; }

        [DataMember(Name = "payload")]
        public string Payload { get; set; }
    }

    [DataContract]
    public class MastodonStatus
    {
        [DataMember(Name = "content")]
        public string Content { get; set; }

        [DataMember(Name = "account")]
        public MastodonAccount Account { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }
    }

    [DataContract]
    public class MastodonAccount
    {
        [DataMember(Name = "display_name")]
        public string Name { get; set; }
    }

    [DataContract]
    public class KnzkLiveLiveInformation {
        [DataMember(Name = "hashtag")]
        public string Hashtag { get; set; }
    }
}
