#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Compression;
using Object = UnityEngine.Object;
using UnityEditor;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System;

namespace NotCat.UdonTools
{
    public class UdonSerializedProgramBytesDecoder : EditorWindow
    {
        public string undecodedProgramString;

        public IUdonProgram decodedUdonProgram;

        public string textArea;

        private Vector2 scrolling1;
        private Vector2 scrolling2;

        [MenuItem("Udon Tools/Udon 反编译")]
        public static void ShowWindow()
        {
            GetWindow<UdonSerializedProgramBytesDecoder>("Udon 程序解码");
        }

        [MenuItem("Assets/Udon Tools/Udon 批量反编译")]
        public static void Execute() {
            if (Selection.assetGUIDs.Count() == 0) {
                Debug.LogError("请选择 Udon 程序资产");
                return;
            }
            BatchDecompileUdon2File(Selection.assetGUIDs);
        }

        static public void BatchDecompileUdon2File(string[] guids)
        {
            Regex regexCompressed = new Regex(@"serializedProgramCompressedBytes:\s*([^ ]+)");
            Regex regex = new Regex(@"serializedProgramBytesString:\s*([^ ]+)");

            foreach(var guid in guids)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"[<color=#0c824c>Udon Program Decoder</color>] File: {filePath}");
                
                string text = File.ReadAllText(filePath);

                Match match = regexCompressed.Match(text);
                IUdonProgram program = null;

                try
                {
                    if (match.Success)
                    {
                        program = DecodeBytes(GZip.Decompress(String2Bytes(match.Groups[1].Value)));
                    }
                    else
                    {
                        match = regex.Match(text);
                        if (match.Success)
                        {
                            program = DecodeBytes(String2Bytes(match.Groups[1].Value));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                    program = null;
                }

                if (program != null)
                {
                    var outputPath = Path.Combine("Temp", "UdonTools", "UdonProgramDecoder", $"udon_guid_{guid}.cs");
                    var timer = new System.Diagnostics.Stopwatch();
                    timer.Start();
                    GenerateCode2File(program, outputPath);
                    Debug.Log($"[<color=#0c824c>Udon Program Decoder</color>] Code generation finished in {timer.Elapsed.ToString(@"ss\.ff")} to {outputPath}");
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(outputPath, -1);
                }
                else
                {
                    Debug.Log($"[<color=#0c824c>Udon Program Decoder</color>] File: {filePath}, Code generation failed");
                }

            }
        }

        public void OnGUI()
        {
            GUILayout.Space(20);
            GUILayout.Label("输入 Udon Program 字符串或者压缩后的 Udon Program 字符串", EditorStyles.boldLabel);
            GUILayout.Space(20);

            scrolling1 = GUILayout.BeginScrollView(scrolling1, GUILayout.Height(100));
            undecodedProgramString = GUILayout.TextArea(undecodedProgramString);
            GUILayout.EndScrollView();
            GUILayout.Space(20);
            
            try
            {
                if (GUILayout.Button("解压并解码"))
                {
                    decodedUdonProgram = DecodeBytes(GZip.Decompress(String2Bytes(undecodedProgramString ?? "")));
                    textArea = GenerateCode2String(decodedUdonProgram);
                }
                if (GUILayout.Button("解码"))
                {
                    decodedUdonProgram = DecodeBytes(String2Bytes(undecodedProgramString ?? ""));
                    textArea = GenerateCode2String(decodedUdonProgram);
                }
            }
            catch
            {
                decodedUdonProgram = null;
                textArea = null;
            }

            if (textArea != null)
            {
                scrolling2 = GUILayout.BeginScrollView(scrolling2);
                // ShowUdonProgram(decodedUdonProgram);
                textArea = GUILayout.TextArea(textArea);
                GUILayout.EndScrollView();
            }
        }

        static public byte[] String2Bytes(string hex)
        {
            return Enumerable.Range(0, hex.Length/2)
                .Select(x => byte.Parse(hex.Substring(2*x, 2), NumberStyles.HexNumber))
                .ToArray();
        }

        static public IUdonProgram DecodeBytes(byte[] serializedProgramBytes)
        {
            if (serializedProgramBytes == null)
            {
                return null;
            }
            else
            {
                return VRC.Udon.Serialization.OdinSerializer.SerializationUtility.DeserializeValue<IUdonProgram>(
                    serializedProgramBytes, DataFormat.Binary, new List<Object>()
                );
            }
        }
        
        static public string GenerateCode2String(IUdonProgram program) {
            var decompiler = new UdonFlat.Decompiler {program=program};
            decompiler.Init();
            decompiler.Translate();
            using (var stream = new MemoryStream())
            {
                stream.Position = 0;
                using (var writer = new StreamWriter(stream))
                {
                    decompiler.GenerateCode(writer);
                    writer.Flush();
                    return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
        }

        static void GenerateCode2File(IUdonProgram program, string outputPath) {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var decompiler = new UdonFlat.Decompiler {program=program, name=Path.GetFileNameWithoutExtension(outputPath)};
            decompiler.Init();
            decompiler.Translate();
            using(var writer = System.IO.File.CreateText(outputPath))
                decompiler.GenerateCode(writer);
        }

        public void ShowUdonProgram(IUdonProgram program)
        {
            textArea = GUILayout.TextArea(program.SymbolTable.GetSymbols().Aggregate((a, b) => a + "\n" + b));
        }
    }
}

#endif