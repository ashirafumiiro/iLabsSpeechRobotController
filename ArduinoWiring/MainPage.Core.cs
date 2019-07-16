using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Maker.RemoteWiring;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;

namespace ArduinoWiring
{
    public sealed partial class MainPage : Page
    {

        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string cmd = args.Hypothesis.Text;

            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ResultText.Text = cmd;
            });
        }

        public async void ListenVoiceCommand()
        {
            await this.dispatcher.RunAsync(CoreDispatcherPriority.High, ()=> {
                ResultText.Text = "Listening ...";
            });
            SpeechRecognizer commandRecognizer = new SpeechRecognizer();
            string[] commands = { "Turn left", "Turn right", "stop moving",
                                   "Move forward", "Move backward",
                                   "how are you", "is every thing fine",
                                   "Lights on", "Lights off", "Red light", "Blue light",
                                   "Green light"
                        };
            var commandConstraints = new SpeechRecognitionListConstraint(commands);

            commandRecognizer.Constraints.Add(commandConstraints);
            await commandRecognizer.CompileConstraintsAsync();
            commandRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;

            SpeechRecognitionResult speechRecognitionResult = await commandRecognizer.RecognizeAsync();

            string robotCommand = speechRecognitionResult.Text;
            if (!robotCommand.Equals(""))
            {
                switch (robotCommand.ToLower())
                {
                    case "how are you":
                        SayWord("I am fine, thank you");
                        break;
                    case "turn left":
                        TurnLeft();
                        SayWord("Turning Left boss");
                        break;
                    case "turn right":
                        TurnRight();
                        SayWord("Vehical turning right");
                        break;
                    case "move backward":
                        SayWord("Moving backward");
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            FrontButton.IsChecked = false;
                            ReverseButton.IsChecked = true;
                        });
                        break;
                    case "move forward":
                        SayWord("moving forward");
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            ReverseButton.IsChecked = false;
                            FrontButton.IsChecked = true;
                        });
                        break;
                    case "stop moving":
                        Stop();
                        break;
                    case "lights on":
                            LightsOn();
                        break;

                    case "lights off":
                            LightsOff();
                        break;
                    case "green light":
                            GreenLight();
                        break;
                    case "red light":
                        RedLight();
                        break;
                    case "blue light":
                        BlueLight();
                        break;
                    default:
                        break;
                }

            }
            else
                SayWord("No valid command specified");
        }

        public async void TurnLeft()
        {
            Stop();
            if (doWrite)
            {
                arduino.digitalWrite(leftEN, PinState.HIGH);
                arduino.digitalWrite(leftInput2, PinState.HIGH);
                arduino.digitalWrite(rightEN, PinState.HIGH);
                arduino.digitalWrite(rightInput1, PinState.HIGH);
                await Task.Delay(500);
            }        
            
            Stop();
        }

        public async void TurnRight()
        {
            Stop();
            if (doWrite)
            {
                arduino.digitalWrite(leftEN, PinState.HIGH);
                arduino.digitalWrite(leftInput1, PinState.HIGH);
                arduino.digitalWrite(rightEN, PinState.HIGH);
                arduino.digitalWrite(rightInput2, PinState.HIGH);
                await Task.Delay(500);
            }
            Stop();
        }

        public void RedLight()
        {
            if (doWrite)
            {
                arduino.digitalWrite(14, PinState.LOW);
                arduino.digitalWrite(15, PinState.HIGH);
                arduino.digitalWrite(16, PinState.LOW);
            }
        }

        public void GreenLight()
        {
            if (doWrite)
            {
                arduino.digitalWrite(14, PinState.HIGH);
                arduino.digitalWrite(15, PinState.LOW);
                arduino.digitalWrite(16, PinState.LOW);
            }
        }

        public void BlueLight()
        {
            if (doWrite)
            {
                arduino.digitalWrite(14, PinState.LOW);
                arduino.digitalWrite(15, PinState.LOW);
                arduino.digitalWrite(16, PinState.HIGH);
            }
        }

        public void LightsOn()
        {
            if (doWrite)
            {
                arduino.digitalWrite(14, PinState.HIGH);
                arduino.digitalWrite(15, PinState.HIGH);
                arduino.digitalWrite(16, PinState.HIGH);
            }
        }

        public void LightsOff()
        {
            if (doWrite)
            {
                arduino.digitalWrite(14, PinState.LOW);
                arduino.digitalWrite(15, PinState.LOW);
                arduino.digitalWrite(16, PinState.LOW);
            }
        }

        public async void SayWord(string word)
        {
            MediaElement mediaElement = new MediaElement();
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            Windows.Media.SpeechSynthesis.SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(word);
            mediaElement.SetSource(stream, stream.ContentType);
            mediaElement.Play();
        }
    }
}
