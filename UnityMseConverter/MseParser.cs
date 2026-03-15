using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace UnityMseConverter
{
    /// <summary>
    /// Metin2 .mse efekt dosyasını parse eder.
    /// Satır bazlı recursive descent parser.
    /// </summary>
    public static class MseParser
    {
        public static MseEffectData ParseFromFile(string filePath)
        {
            string content = File.ReadAllText(filePath);
            return ParseFromString(content);
        }

        public static MseEffectData ParseFromString(string content)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var data = new MseEffectData();
            int index = 0;

            while (index < lines.Length)
            {
                string line = lines[index].Trim();

                if (line.StartsWith("BoundingSphereRadius"))
                {
                    data.BoundingSphereRadius = ParseFloat(GetValue(line));
                }
                else if (line.StartsWith("BoundingSpherePosition"))
                {
                    data.BoundingSpherePosition = ParseVector3FromLine(line);
                }
                else if (line == "Group Particle")
                {
                    index++; // skip to '{'
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    var group = ParseParticleGroup(lines, ref index);
                    data.ParticleGroups.Add(group);
                }

                index++;
            }

            return data;
        }

        private static MseParticleGroup ParseParticleGroup(string[] lines, ref int index)
        {
            var group = new MseParticleGroup();
            int braceDepth = 1;

            while (index < lines.Length && braceDepth > 0)
            {
                string line = lines[index].Trim();

                if (line == "}")
                {
                    braceDepth--;
                    if (braceDepth == 0) break;
                    index++;
                    continue;
                }

                if (line == "{")
                {
                    braceDepth++;
                    index++;
                    continue;
                }

                if (line.StartsWith("StartTime"))
                {
                    group.StartTime = ParseFloat(GetValue(line));
                }
                else if (line == "List TimeEventPosition")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    group.PositionKeyframes = ParsePositionKeyframes(lines, ref index);
                }
                else if (line == "Group EmitterProperty")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    group.EmitterProperty = ParseEmitterProperty(lines, ref index);
                }
                else if (line == "Group ParticleProperty")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    group.ParticleProperty = ParseParticleProperty(lines, ref index);
                }

                index++;
            }

            return group;
        }

        private static MseEmitterProperty ParseEmitterProperty(string[] lines, ref int index)
        {
            var prop = new MseEmitterProperty();
            int braceDepth = 1;

            while (index < lines.Length && braceDepth > 0)
            {
                string line = lines[index].Trim();

                if (line == "}")
                {
                    braceDepth--;
                    if (braceDepth == 0) break;
                    index++;
                    continue;
                }
                if (line == "{")
                {
                    braceDepth++;
                    index++;
                    continue;
                }

                if (line.StartsWith("MaxEmissionCount"))
                    prop.MaxEmissionCount = ParseInt(GetValue(line));
                else if (line.StartsWith("CycleLength"))
                    prop.CycleLength = ParseFloat(GetValue(line));
                else if (line.StartsWith("CycleLoopEnable"))
                    prop.CycleLoopEnable = ParseBool(GetValue(line));
                else if (line.StartsWith("LoopCount"))
                    prop.LoopCount = ParseInt(GetValue(line));
                else if (line.StartsWith("EmitterShape"))
                    prop.EmitterShape = ParseInt(GetValue(line));
                else if (line.StartsWith("EmitterAdvancedType"))
                    prop.EmitterAdvancedType = ParseInt(GetValue(line));
                else if (line.StartsWith("EmittingSize"))
                    prop.EmittingSize = ParseVector3FromLine(line);
                else if (line.StartsWith("EmittingRadius"))
                    prop.EmittingRadius = ParseFloat(GetValue(line));
                else if (line.StartsWith("EmittingDirection"))
                    prop.EmittingDirection = ParseVector3FromLine(line);
                else if (line.StartsWith("EmitFromEdgeFlag"))
                    prop.EmitFromEdgeFlag = ParseBool(GetValue(line));
                else if (line == "List TimeEventEmittingVelocity")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventEmittingVelocity = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventEmissionCountPerSecond")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventEmissionCountPerSecond = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventLifeTime")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventLifeTime = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventSizeX")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventSizeX = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventSizeY")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventSizeY = ParseTimeEventList(lines, ref index);
                }

                index++;
            }

            return prop;
        }

        private static MseParticleProperty ParseParticleProperty(string[] lines, ref int index)
        {
            var prop = new MseParticleProperty();
            int braceDepth = 1;

            while (index < lines.Length && braceDepth > 0)
            {
                string line = lines[index].Trim();

                if (line == "}")
                {
                    braceDepth--;
                    if (braceDepth == 0) break;
                    index++;
                    continue;
                }
                if (line == "{")
                {
                    braceDepth++;
                    index++;
                    continue;
                }

                if (line.StartsWith("SrcBlendType"))
                    prop.SrcBlendType = ParseInt(GetValue(line));
                else if (line.StartsWith("DestBlendType"))
                    prop.DestBlendType = ParseInt(GetValue(line));
                else if (line.StartsWith("ColorOperationType"))
                    prop.ColorOperationType = ParseInt(GetValue(line));
                else if (line.StartsWith("BillboardType"))
                    prop.BillboardType = ParseInt(GetValue(line));
                else if (line.StartsWith("RotationType"))
                    prop.RotationType = ParseInt(GetValue(line));
                else if (line.StartsWith("RotationSpeed"))
                    prop.RotationSpeed = ParseFloat(GetValue(line));
                else if (line.StartsWith("RotationRandomStartBegin"))
                    prop.RotationRandomStartBegin = ParseFloat(GetValue(line));
                else if (line.StartsWith("RotationRandomStartEnd"))
                    prop.RotationRandomStartEnd = ParseFloat(GetValue(line));
                else if (line.StartsWith("AttachEnable"))
                    prop.AttachEnable = ParseBool(GetValue(line));
                else if (line.StartsWith("StretchEnable"))
                    prop.StretchEnable = ParseBool(GetValue(line));
                else if (line.StartsWith("TexAniType"))
                    prop.TexAniType = ParseInt(GetValue(line));
                else if (line.StartsWith("TexAniDelay"))
                    prop.TexAniDelay = ParseFloat(GetValue(line));
                else if (line.StartsWith("TexAniRandomStartFrameFlag"))
                    prop.TexAniRandomStartFrameFlag = ParseBool(GetValue(line));
                else if (line.StartsWith("TexAniFrameCountX"))
                    prop.TexAniFrameCountX = ParseInt(GetValue(line));
                else if (line.StartsWith("TexAniFrameCountY"))
                    prop.TexAniFrameCountY = ParseInt(GetValue(line));
                else if (line == "List TimeEventGravity")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventGravity = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventAirResistance")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventAirResistance = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventScaleX")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventScaleX = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventScaleY")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventScaleY = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventColorRed")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventColorRed = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventColorGreen")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventColorGreen = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventColorBlue")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventColorBlue = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventAlpha")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventAlpha = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TimeEventRotation")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TimeEventRotation = ParseTimeEventList(lines, ref index);
                }
                else if (line == "List TextureFiles")
                {
                    index++;
                    index = SkipToOpenBrace(lines, index);
                    index++;
                    prop.TextureFiles = ParseTextureFileList(lines, ref index);
                }

                index++;
            }

            return prop;
        }

        private static List<MseTimeEvent> ParseTimeEventList(string[] lines, ref int index)
        {
            var events = new List<MseTimeEvent>();

            while (index < lines.Length)
            {
                string line = lines[index].Trim();
                if (line == "}")
                    break;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    events.Add(new MseTimeEvent(ParseFloat(parts[0]), ParseFloat(parts[1])));
                }

                index++;
            }

            return events;
        }

        private static List<MsePositionKeyframe> ParsePositionKeyframes(string[] lines, ref int index)
        {
            var keyframes = new List<MsePositionKeyframe>();

            while (index < lines.Length)
            {
                string line = lines[index].Trim();
                if (line == "}")
                    break;

                // Format: time "MOVING_TYPE_DIRECT" x y z
                var kf = new MsePositionKeyframe();
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 5)
                {
                    kf.Time = ParseFloat(parts[0]);
                    kf.MovingType = parts[1].Trim('"');
                    kf.Position = new MseVector3(
                        ParseFloat(parts[2]),
                        ParseFloat(parts[3]),
                        ParseFloat(parts[4])
                    );
                    keyframes.Add(kf);
                }

                index++;
            }

            return keyframes;
        }

        private static List<string> ParseTextureFileList(string[] lines, ref int index)
        {
            var files = new List<string>();

            while (index < lines.Length)
            {
                string line = lines[index].Trim();
                if (line == "}")
                    break;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Tırnak işaretlerini temizle
                    files.Add(line.Trim('"'));
                }

                index++;
            }

            return files;
        }

        #region Yardımcı Metodlar

        private static int SkipToOpenBrace(string[] lines, int index)
        {
            while (index < lines.Length)
            {
                if (lines[index].Trim() == "{")
                    return index;
                index++;
            }
            return index;
        }

        private static string GetValue(string line)
        {
            int spaceIdx = line.IndexOf(' ');
            if (spaceIdx < 0)
                spaceIdx = line.IndexOf('\t');
            if (spaceIdx < 0)
                return "";
            return line.Substring(spaceIdx).Trim();
        }

        private static MseVector3 ParseVector3FromLine(string line)
        {
            string value = GetValue(line);
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                return new MseVector3(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2]));
            return new MseVector3();
        }

        private static float ParseFloat(string s)
        {
            if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return 0f;
        }

        private static int ParseInt(string s)
        {
            if (int.TryParse(s.Trim(), out int result))
                return result;
            return 0;
        }

        private static bool ParseBool(string s)
        {
            string v = s.Trim().ToLower();
            return v == "1" || v == "true";
        }

        #endregion
    }
}
