using System;
using Google.Cloud.TextToSpeech.V1;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DynamisBridge
{
    internal class Google
    {
        public static TextToSpeechClient ttsClient = TextToSpeechClient.Create();

        static void Main(string[] args)
        {
            
        }

        public static async Task<string> CreateAudioFile(string text)
        {
            var input = new SynthesisInput
            {
                Text = text
            };

            var voiceSelection = new VoiceSelectionParams
            {
                LanguageCode = "en-US",
                Name = "en-US-Wavenet-F",
                SsmlGender = SsmlVoiceGender.Female
            };

            var audioConfig = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Mp3
            };

            var response = await ttsClient.SynthesizeSpeechAsync(input, voiceSelection, audioConfig);

            using var output = File.Create(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, $"audio-{DateTime.Now.ToFileTime()}.mp3"));
            response.AudioContent.WriteTo(output);
            Plugin.Logger.Debug($"Audio content written to {output.Name}");
            DeleteOldFiles();
            return output.Name;
        }

        private static void DeleteOldFiles()
        {
            try
            {

                var dir = new DirectoryInfo(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? Directory.GetCurrentDirectory()));
                var files = dir.GetFiles("audio-*.mp3");

                if (files.Length <= 10)
                    return;

                var filesToDelete = files.OrderByDescending(file => file.CreationTime).Skip(10).ToList();
                filesToDelete.ForEach(file =>
                {
                    Plugin.Logger.Debug($"Deleted {file.Name}!");
                    file.Delete();
                });
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"Error deleting files: {ex.Message}");
            }
        }
    }
}
