using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace 黑市
{
    public class NPC掉落配置
    {
        public static readonly string 配置路径 = Path.Combine(黑市路径.根目录, "NPC掉落.json");

        [JsonProperty("击杀浮动文本")] public bool 击杀浮动文本 { get; set; } = true;
        [JsonProperty("击杀聊天栏文本")] public bool 击杀聊天栏文本 { get; set; } = true;

        [JsonProperty("掉落规则")]
        public List<NPC掉落规则> 掉落规则列表 { get; set; } = new();

        public static NPC掉落配置 加载(out string 错误报告)
        {
            错误报告 = "";
            黑市路径.确保目录存在();
            if (!File.Exists(配置路径))
            {
                var 默认 = 创建默认配置();
                默认.保存();
                return 默认;
            }
            try
            {
                var json = File.ReadAllText(配置路径);

                // 严格验证 JSON 格式完整性
                try { JToken.Parse(json); }
                catch (Exception jsonEx)
                {
                    错误报告 = "NPC掉落.json JSON格式错误：" + jsonEx.Message;
                    return new NPC掉落配置();
                }

                var 配置 = JsonConvert.DeserializeObject<NPC掉落配置>(json);
                if (配置 == null)
                {
                    错误报告 = "NPC掉落.json 反序列化失败，请检查JSON格式。";
                    return new NPC掉落配置();
                }

                bool 验证通过 = true;
                var 报告列表 = new List<string>();

                if (配置.掉落规则列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("掉落规则列表不能为空。");
                }
                else
                {
                    var id集合 = new HashSet<int>();
                    foreach (var 规则 in 配置.掉落规则列表)
                    {
                        if (规则 == null)
                        {
                            验证通过 = false;
                            报告列表.Add("发现空掉落规则。");
                            continue;
                        }
                        if (规则.NPCID <= 0)
                        {
                            验证通过 = false;
                            报告列表.Add($"发现非法NPCID：{规则.NPCID}。");
                            continue;
                        }
                        if (id集合.Contains(规则.NPCID))
                        {
                            验证通过 = false;
                            报告列表.Add($"发现重复NPCID：{规则.NPCID}。");
                            continue;
                        }
                        id集合.Add(规则.NPCID);

                        if (规则.掉落数量 < 0)
                        {
                            验证通过 = false;
                            报告列表.Add($"NPCID {规则.NPCID} 掉落数量不能为负。");
                        }
                        if (string.IsNullOrWhiteSpace(规则.货币名称))
                        {
                            验证通过 = false;
                            报告列表.Add($"NPCID {规则.NPCID} 货币名称不能为空。");
                        }
                        if (string.IsNullOrWhiteSpace(规则.NPC名称))
                        {
                            验证通过 = false;
                            报告列表.Add($"NPCID {规则.NPCID} NPC名称不能为空。");
                        }
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new NPC掉落配置();
                }

                // 验证通过，整理并保存
                配置.保存();
                return 配置;
            }
            catch (Exception ex)
            {
                错误报告 = $"NPC掉落.json 加载异常：{ex.Message}";
                return new NPC掉落配置();
            }
        }

        public static NPC掉落配置 加载()
        {
            return 加载(out _);
        }

        private static NPC掉落配置 创建默认配置()
        {
            var 默认 = new NPC掉落配置();
            默认.掉落规则列表.Add(new NPC掉落规则 { NPCID = 4, NPC名称 = "克苏鲁之眼", 货币名称 = "功勋", 掉落数量 = 10 });
            默认.掉落规则列表.Add(new NPC掉落规则 { NPCID = 1, NPC名称 = "史莱姆", 货币名称 = "声望", 掉落数量 = 3 });
            return 默认;
        }

        public void 保存()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var token = JToken.FromObject(this, JsonSerializer.Create(settings));
            var sb = new StringBuilder();
            写入格式化(token, sb, 0, false, 0);

            File.WriteAllText(配置路径, sb.ToString());
        }

        private static bool 对象应该横向(JToken token, bool 父是数组, int 深度)
        {
            if (父是数组) return true;
            if (深度 == 1 && token.Type == JTokenType.Object) return true;
            if (token.Type == JTokenType.Object && ((JObject)token).Count == 0) return true;
            return false;
        }

        private static bool 数组应该横向(JToken token, bool 父是数组, int 深度)
        {
            if (token is JArray arr && arr.Count == 0) return true;

            if (!父是数组 && token is JArray arr2 && arr2.Count <= 3)
            {
                bool 全简单 = arr2.All(t =>
                    t.Type == JTokenType.String ||
                    t.Type == JTokenType.Integer ||
                    t.Type == JTokenType.Float ||
                    t.Type == JTokenType.Boolean);
                if (全简单) return true;
            }

            return false;
        }

        private static void 写入格式化(JToken token, StringBuilder sb, int indent, bool 父是数组, int 深度)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    if (obj.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }

                    bool 对象横向 = 对象应该横向(token, 父是数组, 深度);

                    sb.Append("{");

                    bool first = true;
                    foreach (var prop in obj.Properties())
                    {
                        if (!first)
                        {
                            sb.Append(对象横向 ? ", " : ",");
                            if (!对象横向) sb.AppendLine();
                        }
                        first = false;

                        if (!对象横向)
                            sb.Append(new string(' ', (indent + 1) * 2));

                        sb.Append("\"");
                        sb.Append(prop.Name);
                        sb.Append("\": ");

                        写入格式化(prop.Value, sb, indent + 1, false, 深度 + 1);
                    }

                    if (!对象横向)
                    {
                        sb.AppendLine();
                        sb.Append(new string(' ', indent * 2));
                    }
                    sb.Append("}");
                    break;

                case JTokenType.Array:
                    var arr = (JArray)token;
                    if (arr.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }

                    bool 数组横向 = 数组应该横向(token, 父是数组, 深度);

                    if (数组横向)
                    {
                        sb.Append("[");
                        for (int i = 0; i < arr.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            写入格式化(arr[i], sb, indent, true, 深度 + 1);
                        }
                        sb.Append("]");
                    }
                    else
                    {
                        sb.AppendLine("[");
                        for (int i = 0; i < arr.Count; i++)
                        {
                            sb.Append(new string(' ', (indent + 1) * 2));
                            写入格式化(arr[i], sb, indent + 1, true, 深度 + 1);
                            if (i < arr.Count - 1) sb.Append(",");
                            sb.AppendLine();
                        }
                        sb.Append(new string(' ', indent * 2));
                        sb.Append("]");
                    }
                    break;

                case JTokenType.String:
                    sb.Append(JsonConvert.ToString(token.Value<string>()));
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                    sb.Append(token.ToString());
                    break;

                case JTokenType.Boolean:
                    sb.Append(token.Value<bool>() ? "true" : "false");
                    break;

                case JTokenType.Null:
                    sb.Append("null");
                    break;

                default:
                    sb.Append(token.ToString());
                    break;
            }
        }
    }

    public class NPC掉落规则
    {
        [JsonProperty("NPCID")] public int NPCID { get; set; }
        [JsonProperty("NPC名称")] public string NPC名称 { get; set; } = "";
        [JsonProperty("货币名称")] public string 货币名称 { get; set; } = "";
        [JsonProperty("掉落数量")] public int 掉落数量 { get; set; } = 1;
    }
}