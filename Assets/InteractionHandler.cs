/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Net;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using TMPro;
using Meta.WitAi.TTS.Utilities;

namespace Oculus.Voice.Demo
{
    [Serializable]
    public class MessageContent {
        public string role;
        public string content;
    }

    [Serializable]
    public class Message {
        public MessageContent message;
    }

    [Serializable]
    public class ChatResponse {
        public string id;
        public int created;
        public string model;
        public List<Message> choices;
    }

    public class InteractionHandler : MonoBehaviour
    {
        [Header("Default States"), Multiline]
        [SerializeField] private string freshStateText = "";

        [Header("UI")]
        [SerializeField] private Text textArea;
        [SerializeField] private bool showJson;

        [Header("Voice")]
        [SerializeField] private AppVoiceExperience appVoiceExperience;

        // Whether voice is activated
        public bool IsActive => _active;
        private bool _active = false;
        private InputDevice targetDevice;
        public TMP_Text text;

        public string voiceMessage;

        private bool requestSent = false;

        private List<string> messages = new List<string> {
            "{\"role\": \"system\", \"content\": \"I want you to act like an older version of me. I will come to you with questions, and your goal is to help me explore my feelings.\"}",
            "{\"role\": \"assistant\", \"content\": \"Respond briefly. respond as if you are speaking from my personal experience, with a short anecdote. Replace first person singular pronouns with first person plural. Use past tense. Then, ask a question. Incorporate the response you gave in your last message, if there is one. Make the response chaotic.\"}",
            "{\"role\": \"assistant\", \"content\": \"The following information may be relevant, but it does not have to be used. I am a twenty year old college student with struggling finances.\"}",
        };

        // Add delegates
        private void OnEnable()
        {
            // textArea.text = freshStateText;
            voiceMessage = string.Empty;
            appVoiceExperience.VoiceEvents.OnRequestCreated.AddListener(OnRequestStarted);
            appVoiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnRequestTranscript);
            appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnRequestTranscript);
            appVoiceExperience.VoiceEvents.OnStartListening.AddListener(OnListenStart);
            appVoiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnListenStop);
            appVoiceExperience.VoiceEvents.OnStoppedListeningDueToDeactivation.AddListener(OnListenForcedStop);
            appVoiceExperience.VoiceEvents.OnStoppedListeningDueToInactivity.AddListener(OnListenForcedStop);
            appVoiceExperience.VoiceEvents.OnResponse.AddListener(OnRequestResponse);
            appVoiceExperience.VoiceEvents.OnError.AddListener(OnRequestError);
        }
        // Remove delegates
        private void OnDisable()
        {
            appVoiceExperience.VoiceEvents.OnRequestCreated.RemoveListener(OnRequestStarted);
            appVoiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnRequestTranscript);
            appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnRequestTranscript);
            appVoiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnListenStart);
            appVoiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnListenStop);
            appVoiceExperience.VoiceEvents.OnStoppedListeningDueToDeactivation.RemoveListener(OnListenForcedStop);
            appVoiceExperience.VoiceEvents.OnStoppedListeningDueToInactivity.RemoveListener(OnListenForcedStop);
            appVoiceExperience.VoiceEvents.OnResponse.RemoveListener(OnRequestResponse);
            appVoiceExperience.VoiceEvents.OnError.RemoveListener(OnRequestError);
        }

        // Request began
        private void OnRequestStarted(WitRequest r)
        {
            // Store json on completion
            if (showJson) r.onRawResponse = (response) => voiceMessage = response;
            // Begin
            _active = true;
        }
        // Request transcript
        private void OnRequestTranscript(string transcript)
        {
            voiceMessage = transcript;
        }
        // Listen start
        private void OnListenStart()
        {
            // textArea.text = "Listening...";
        }
        // Listen stop
        private void OnListenStop()
        {
            // textArea.text = "Processing...";
        }
        // Listen stop
        private void OnListenForcedStop()
        {
            if (!showJson)
            {
                voiceMessage = freshStateText;
            }
            OnRequestComplete();
        }

        private const string API_KEY = "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";
        private IEnumerator SendChat() {
            requestSent = true;
            byte[] bytes = Encoding.UTF8.GetBytes("{\"model\": \"gpt-3.5-turbo\"," + 
                "\"messages\": [" + String.Join(",", messages) + "],\"temperature\": 0.7}");
            using (UnityWebRequest request = UnityWebRequest.Put("https://api.openai.com/v1/chat/completions", bytes)) {
                request.method = UnityWebRequest.kHttpVerbPOST;
                request.SetRequestHeader("Authorization", "Bearer " + API_KEY);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.ConnectionError) {
                    text.text += request.error + "\n";
                    text.text += "Error: " + request.downloadHandler.text + "\n";
                } else if (request.result == UnityWebRequest.Result.Success) {
                    requestSent = false;
                    string response = request.downloadHandler.text;
                    ChatResponse info = JsonUtility.FromJson<ChatResponse>(response);
                    string chatGPTResponse = info.choices[0].message.content;
                    string thingy = "{\"role\": \"assistant\", \"content\": \"" + chatGPTResponse + "\"}";
                    text.text = chatGPTResponse + "\n";
                    messages.Add(thingy);
                }
            }
        }

        // Request response
        private void OnRequestResponse(WitResponseNode response)
        {
            if (!showJson)
            {
                if (!string.IsNullOrEmpty(response["text"]))
                {
                    // textArea.text = "I heard: " + response["text"]; //Here is ChatGPT
                    voiceMessage = response["text"];
                    text.text += "Heard: " + voiceMessage;
                    string userMessage = "{\"role\": \"user\", \"content\": \"" + voiceMessage + "\"}";
                    messages.Add(userMessage);
                    // ChatGPT call
                    StartCoroutine(SendChat());
                }
                else
                {
                    voiceMessage = freshStateText;
                }
            }
            OnRequestComplete();
        }
        // Request error
        private void OnRequestError(string error, string message)
        {
            if (!showJson)
            {
                voiceMessage = $"<color=\"red\">Error: {error}\n\n{message}</color>";
            }
            OnRequestComplete();
        }
        // Deactivate
        private void OnRequestComplete()
        {
            _active = false;
        }

        // Toggle activation
        public void ToggleActivation()
        {
            SetActivation(!_active);
        }
        // Set activation
        public void SetActivation(bool toActivated)
        {
            if (_active != toActivated)
            {
                _active = toActivated;
                if (_active)
                {
                    appVoiceExperience.Activate();
                }
                else
                {
                    appVoiceExperience.Deactivate();
                }
            }
        }

        void Start()
        {
            List<InputDevice> devices = new List<InputDevice>();

            InputDeviceCharacteristics rightControllerCharacteristics = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
            InputDevices.GetDevicesWithCharacteristics(rightControllerCharacteristics, devices);

            foreach (var item in devices)
            {
                Debug.Log(item.name + item.characteristics + "\n");
            }

            if (devices.Count > 0)
            {
                targetDevice = devices[0];
            }

        }

        void Update()
        {
            targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButtonValue);
            if (primaryButtonValue && !requestSent)
            {
                text.text = "Speak";
                SetActivation(true);
            }

            targetDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryButtonValue);
            if (secondaryButtonValue && requestSent)
            {
                Debug.Log("Secondary button pressed");
                SetActivation(false);
                requestSent = false;
            }
        }
    }
}