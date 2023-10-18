﻿    using Gallop;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UmamusumeResponseAnalyzer.Game;

namespace UmamusumeResponseAnalyzer.AI
{
    public class GameStatusSend
    {
        public int umaId;//马娘编号，见KnownUmas.cpp
        public int turn;//回合数，从0开始，到77结束
        public int vital;//体力，叫做“vital”是因为游戏里就这样叫的
        public int maxVital;//体力上限
        public bool isQieZhe;//切者
        public bool isAiJiao;//爱娇
        public int failureRateBias;//失败率改变量。练习上手=2，练习下手=-2
        public int[] fiveStatus;//五维属性，1200以上不减半
                                //int fiveStatusUmaBonus[5];//马娘自身加成
        public int[] fiveStatusLimit;//五维属性上限，1200以上不减半
        public int skillPt;//技能点
        public int motivation;//干劲，从1到5分别是绝不调到绝好调
        public int[] cardId;//6张卡的id
        public int[] cardJiBan;//羁绊，六张卡分别012345，理事长6，记者7
        public int[] trainLevelCount;//五个训练的等级的计数，实际训练等级=min(5,t/12+1)
        public int[] zhongMaBlueCount;//种马的蓝因子个数，假设只有3星
        public int[] zhongMaExtraBonus;//种马的剧本因子以及技能白因子（等效成pt），每次继承加多少。全大师杯因子典型值大约是30速30力200pt
        public bool isRacing;//这个回合是否在比赛
        // 额外信息
        public int fans; // 粉丝数，用于计算固有
        public bool friendCardFirstClick;       // 是否点击过友人
        public bool friendCardUnlockOutgoing; // 友人卡是否出门
        public int friendCardOutgoingStage;   // 友人卡（假设只有一张）使用过的出行次数

        //女神杯相关
        public VenusDataSet venusData;
        // LArc
        public LArcDataSet larcData;

        //当前回合的训练信息
        //0支援卡还未分配，1支援卡分配完毕或比赛开始前，2训练结束后或比赛结束后，0检查各种固定事件与随机事件并进入下一个回合
        //stageInTurn=0时可以输入神经网络输出估值，stageInTurn=1时可以输入神经网络输出policy
        public int stageInTurn;
        public bool[,] cardDistribution;//支援卡分布，六张卡分别012345，理事长6，记者7
        public bool[] cardHint;//六张卡分别有没有亮红点

        //通过计算获得的信息
        public int[,] trainValue;//第一个数是第几个训练，第二个数依次是速耐力根智pt体力
        public int[] failRate;//训练失败率
        
        public GameStatusSend(Gallop.SingleModeCheckEventResponse @event)
        {

            if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0) || @event.data.race_start_info != null) return;

            stageInTurn = 1;

            umaId = @event.data.chara_info.card_id + 1000000 * @event.data.chara_info.rarity;
            turn = @event.data.chara_info.turn - 1;
            vital = @event.data.chara_info.vital;
            maxVital = @event.data.chara_info.max_vital;
            isQieZhe = @event.data.chara_info.chara_effect_id_array.Contains(7);
            isAiJiao = @event.data.chara_info.chara_effect_id_array.Contains(8);
            failureRateBias = 0;
            if (@event.data.chara_info.chara_effect_id_array.Contains(6))
            {
                failureRateBias = 2;
            }
            if (@event.data.chara_info.chara_effect_id_array.Contains(10))
            {
                failureRateBias = -2;
            }


            fiveStatus = new int[]
            {
                ScoreUtils.ReviseOver1200(@event.data.chara_info.speed),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.stamina),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.power) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.guts) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.wiz) ,
            };

            fiveStatusLimit = new int[]
            {
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_speed),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_stamina),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_power) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_guts) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_wiz) ,
            };


            skillPt = @event.data.chara_info.skill_point;

            try
            {
                double ptRate = isQieZhe ? 2.0 : 1.8;
                double ptScore = AiUtils.calculateSkillScore(@event, ptRate);
                skillPt=(int)(ptScore/ptRate);
            }
            catch(Exception ex)
            {
                AnsiConsole.MarkupLine("获取当前技能分失败"+ex.Message);
            }

            motivation = @event.data.chara_info.motivation;
            fans = @event.data.chara_info.fans;
            cardId = new int[6];
            cardJiBan = new int[8];
            
            foreach (var s in @event.data.chara_info.support_card_array)
            {
                int p = s.position - 1;
                //100000*突破数+卡原来的id，例如神团是30137，满破神团就是430137
                cardId[p] = s.limit_break_count * 100000 + s.support_card_id;
            }
            
            foreach (var s in @event.data.chara_info.evaluation_info_array)
            {
                int p=s.target_id - 1;
                if (p < 6)//支援卡
                {
                    cardJiBan[p] = s.evaluation;
                }
                else if (p == 101)//理事长
                {
                    cardJiBan[6] = s.evaluation;
                }
                else if (p == 102)//记者
                {
                    cardJiBan[7] = s.evaluation;
                }
            }
            {
                //难以判断实际计数是多少，所以直接视为半级
                int[] trainLevelCountEachLevel = new int[6] { 0, 6, 18, 30, 42, 48 };
                trainLevelCount = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    trainLevelCount[i] = trainLevelCountEachLevel[@event.data.chara_info.training_level_info_array.First(x => x.command_id == GameGlobal.TrainIds[i]).level];
                }
            }

            zhongMaBlueCount = new int[5];
            //用属性上限猜蓝因子个数
            {
                int[] defaultLimit = new int[5] { 1800, 1600, 1800, 1400, 1400 };
                double factor = 16;//每个三星因子可以提多少上限
                if (turn >= 54)//第二次继承结束
                    factor = 22;
                else if (turn >= 30)//第二次继承结束
                    factor = 19;
                for (int i = 0; i < 5; i++)
                {
                    int threeStarCount = (int)Math.Round((fiveStatusLimit[i] - defaultLimit[i]) / 2 / factor);
                    if (threeStarCount > 6) threeStarCount = 6;
                    if (threeStarCount < 0) threeStarCount = 0;
                    zhongMaBlueCount[i] = threeStarCount * 3;
                }
            }
            zhongMaExtraBonus = new int[6] { 20, 0, 40, 0, 20, 150 };//大师杯和青春杯因子各一半

            cardDistribution = new bool[5, 8];
            cardHint=new bool[6] {false,false,false,false,false,false};
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 8; j++)
                    cardDistribution[i, j] = false;
            isRacing = true;
            foreach (var train in @event.data.home_info.command_info_array)
            {
                if (!GameGlobal.ToTrainIndex.ContainsKey(train.command_id))//不是正常训练
                    continue;
                int trainId = GameGlobal.ToTrainIndex[train.command_id];
                if (train.is_enable > 0) isRacing = false;
                foreach(var p in train.training_partner_array)
                {
                    int pid = p <= 6 ? p - 1 : p == 102 ? 6 : 7;
                    cardDistribution[trainId, pid] = true;
                }
                foreach (var p in train.tips_event_partner_array)
                {
                    int pid = p - 1;
                    cardHint[pid] = true;
                }
            }

            trainValue = new int[5, 7];
            failRate = new int[5];
            {
                var currentVital = @event.data.chara_info.vital;
                //maxVital = @event.data.chara_info.max_vital;
                var currentFiveValue = fiveStatus;

                var trainItems = new Dictionary<int, SingleModeCommandInfo>();
                if (@event.IsScenario(ScenarioType.LArc))
                {
                    //LArc的合宿ID不一样，所以要单独处理
                    trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 1101) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1101) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                    trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 1102) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1102) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                    trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 1103) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1103) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                    trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 1104) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1104) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                    trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 1105) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1105) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
                }
                else
                {
                    //速耐力根智，6xx为合宿时ID
                    trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 601) ? @event.data.home_info.command_info_array.First(x => x.command_id == 601) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                    trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 602) ? @event.data.home_info.command_info_array.First(x => x.command_id == 602) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                    trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 603) ? @event.data.home_info.command_info_array.First(x => x.command_id == 603) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                    trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 604) ? @event.data.home_info.command_info_array.First(x => x.command_id == 604) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                    trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 605) ? @event.data.home_info.command_info_array.First(x => x.command_id == 605) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
                }

                var trainStats = new TrainStats[5];
                var failureRate = new Dictionary<int, int>();
                for (int t = 0; t < 5; t++)
                {
                    int tid = GameGlobal.TrainIds[t];
                    failureRate[tid] = trainItems[tid].failure_rate;
                    var trainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                    var nonScenarioTrainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                    //去掉剧本加成的训练值（游戏里的下层显示）
                    foreach (var item in @event.data.home_info.command_info_array)
                    {
                        if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                nonScenarioTrainParams[trainParam.target_type] += trainParam.value;
                                trainParams[trainParam.target_type] += trainParam.value;
                            }
                        }
                    }

                    //青春杯
                    if (@event.data.team_data_set != null)
                    {
                        foreach (var item in @event.data.team_data_set.command_info_array)
                        {
                            if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                            {
                                foreach (var trainParam in item.params_inc_dec_info_array)
                                {
                                    trainParams[trainParam.target_type] += trainParam.value;
                                    //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                                }
                            }
                        }
                    }
                    //巅峰杯
                    if (@event.data.free_data_set != null)
                    {
                        foreach (var item in @event.data.free_data_set.command_info_array)
                        {
                            if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                            {
                                foreach (var trainParam in item.params_inc_dec_info_array)
                                {
                                    trainParams[trainParam.target_type] += trainParam.value;
                                    //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                                }
                            }
                        }
                    }
                    //偶像杯
                    if (@event.data.live_data_set != null)
                    {
                        foreach (var item in @event.data.live_data_set.command_info_array)
                        {
                            if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                            {
                                foreach (var trainParam in item.params_inc_dec_info_array)
                                {
                                    trainParams[trainParam.target_type] += trainParam.value;
                                    //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                                }
                            }
                        }
                    }
                    //女神杯
                    if (@event.IsScenario(ScenarioType.GrandMasters))
                    {
                        venusData = new VenusDataSet(@event);
                        foreach (var item in @event.data.venus_data_set.command_info_array)
                        {
                            if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                            {
                                foreach (var trainParam in item.params_inc_dec_info_array)
                                {
                                    trainParams[trainParam.target_type] += trainParam.value;
                                }
                            }
                        }
                    }
                    //凯旋门
                    if (@event.IsScenario(ScenarioType.LArc))
                    {
                        larcData = new LArcDataSet(@event);
                        foreach (var item in @event.data.arc_data_set.command_info_array)
                        {
                            if (GameGlobal.ToTrainId.ContainsKey(item.command_id) &&
                                GameGlobal.ToTrainId[item.command_id] == tid)
                            {
                                foreach (var trainParam in item.params_inc_dec_info_array)
                                {
                                    trainParams[trainParam.target_type] += trainParam.value;
                                }
                            }
                        }
                    }

                    var stats = new TrainStats();
                    stats.FailureRate = trainItems[tid].failure_rate;
                    stats.VitalGain = trainParams[10];
                    if (currentVital + stats.VitalGain > maxVital)
                        stats.VitalGain = maxVital - currentVital;
                    if (stats.VitalGain < -currentVital)
                        stats.VitalGain = -currentVital;
                    stats.FiveValueGain = new int[] { trainParams[1], trainParams[2], trainParams[3], trainParams[4], trainParams[5] };
                    for (int i = 0; i < 5; i++)
                        stats.FiveValueGain[i] = ScoreUtils.ReviseOver1200(currentFiveValue[i] + stats.FiveValueGain[i]) - ScoreUtils.ReviseOver1200(currentFiveValue[i]);
                    stats.PtGain = trainParams[30];
                    stats.FiveValueGainNonScenario = new int[] { nonScenarioTrainParams[1], nonScenarioTrainParams[2], nonScenarioTrainParams[3], nonScenarioTrainParams[4], nonScenarioTrainParams[5] };
                    for (int i = 0; i < 5; i++)
                        stats.FiveValueGainNonScenario[i] = ScoreUtils.ReviseOver1200(currentFiveValue[i] + stats.FiveValueGainNonScenario[i]) - ScoreUtils.ReviseOver1200(currentFiveValue[i]);
                    stats.PtGainNonScenario = nonScenarioTrainParams[30];
                    for (int i = 0; i < 5; i++)
                        trainValue[t, i] = stats.FiveValueGain[i];
                    trainValue[t, 5] = stats.PtGain;
                    trainValue[t, 6] = stats.VitalGain;
                    failRate[t] = stats.FailureRate;
                }
            }
        }    
    }
}
