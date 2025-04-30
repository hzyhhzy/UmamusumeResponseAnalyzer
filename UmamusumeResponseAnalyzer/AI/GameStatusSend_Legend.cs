using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UmamusumeResponseAnalyzer.Game.TurnInfo;
using UmamusumeResponseAnalyzer.Game;
using static UmamusumeResponseAnalyzer.NameManager;
using Spectre.Console;
using System.Collections.Frozen;
using Newtonsoft.Json;
using UmamusumeResponseAnalyzer.Communications.Subscriptions;
using UmamusumeResponseAnalyzer.Handler;

namespace UmamusumeResponseAnalyzer.AI
{
    public class ScenarioBuffInfo_Legend
    {
        public int buffId;
        public bool isActive;
        public int coolTime;
        public ScenarioBuffInfo_Legend()
        {
            buffId = -1;
            isActive = false;
            coolTime = 0;
        }
    }
    public class GameStatusSend_Legend
    {
        public bool islegal;//是否为有效的回合数据
        public int scenarioId;  // 剧本编号
        public string protocolVersion; // 通信数据版本

        public int umaId;//马娘编号，见KnownUmas.cpp
        public int umaStar;//几星

        public int turn;//回合数，从0开始，到77结束
        public int vital;//体力，叫做“vital”是因为游戏里就这样叫的
        public int maxVital;//体力上限
        public int motivation;//干劲，从1到5分别是绝不调到绝好调

        public int[] fiveStatus;//五维属性，1200以上不减半
        public int[] fiveStatusLimit;//五维属性上限，1200以上不减半
        public int skillPt;//技能点
        public int skillScore;//已买技能的分数
        //public int hintSkillLvCount;//已经有多少级hint的技能了。hintSkillLvCount越多，hint出技能的概率越小，出属性的概率越大。
        public int[] trainLevelCount;

        //public double ptScoreRate;
        public int failureRateBias;//失败率改变量。练习上手=2，练习下手=-2
        public bool isQieZhe;//切者
        public bool isAiJiao;//爱娇
        public bool isPositiveThinking;//ポジティブ思考，友人第三段出行选上的buff，可以防一次掉心情
        public bool isRefreshMind;//休息的心得,每回合体力+5

        public int[] zhongMaBlueCount;//种马的蓝因子个数，假设只有3星
        public int[] zhongMaExtraBonus;//种马的剧本因子以及技能白因子（等效成pt），每次继承加多少。全大师杯因子典型值大约是30速30力200pt

        public int stage; // 回合类型，2为正常训练回合（含比赛），4为选择团卡事件前，6为选择心得前
        public int decidingEvent;// 需要处理的含选择项的事件。选buff 1，团卡出行 2，团卡三选一 3
        public bool isRacing;

        public int friendship_noncard_yayoi;//非卡理事长的羁绊，带了理事长卡就是0
        public int friendship_noncard_reporter;//非卡记者的羁绊       

        public int[] cardId;
        public PersonBase[] persons;//依次是6张卡
        public int[,] personDistribution;//每个训练有哪些人头id，personDistribution[哪个训练][第几个人头]，空人头为-1
        public int lockedTrainingId;
        
        public int saihou;

        //剧本相关--------------------------------------------------------------------------------------
        public int lg_mainColor;// 主色
        public int[] lg_gauge; // 三种颜色的格数
        public int[] lg_trainingColor; // 训练的gauge颜色
        //public bool[] lg_trainingColorBoost;// 训练的gauge是否+3
        public ScenarioBuffInfo_Legend[] lg_buffs;// 10个buff，空则buffId=0
        public bool[] lg_haveBuff; // 有哪些buff，和lg_buffs重复但是便于查找
        public int lg_pickedBuffsNum;// 抽取到了几个buff
        public int[] lg_pickedBuffs;// 抽取到的buff的id

        public bool lg_blue_active;// 蓝登的超绝好调
        public int lg_blue_remainCount;// 超绝好调还剩几个回合
        public int lg_blue_currentStepCount;// 满3格启动超绝好调
        public int lg_blue_canExtendCount;// 还能延长几次

        public bool lg_green_active;
        public int lg_green_continuationZoneCount; 
        public int lg_green_currentStepCount;

        public int[] lg_red_friendsGauge;// 红登的羁绊条，编号和personIdEnum对应
        public int[] lg_red_friendsLv;// 红登的等级条，编号和personIdEnum对应

        //单独处理剧本友人/团队卡，因为接近必带。其他友人团队卡的以后再考虑
        public int friend_type;//0没带友人卡，1 ssr卡，2 r卡
        public int friend_personId;//友人卡在persons里的编号
        //double friend_vitalBonus;//友人卡的回复量倍数
        //double friend_statusBonus;//友人卡的事件效果倍数
        public int friend_stage;//0是未点击，1是已点击但未解锁出行，2是已解锁出行
        public bool[] friend_outgoingUsed;//友人的出行哪几段走过了   暂时不考虑其他友人团队卡的出行
        public bool friend_qingre;//团卡是否情热
        public int friend_qingreTurn;//团卡连续情热多少回合了

        public GameStatusSend_Legend(Gallop.SingleModeCheckEventResponse @event)
        {
            islegal = true;
            scenarioId = 10;
            protocolVersion = "250405";

            stage = Handlers.GetCommandInfoStage_legend(@event);
            if (stage == 0)
            {
                islegal = false;
                return;
            }
            decidingEvent = 0;

            if (stage == 5)
            {
                decidingEvent = 1;//买心得
            }

            if (stage == 3)
            {
                if(@event.data.unchecked_event_array.Any(x => x.story_id == 830241003))
                    decidingEvent = 3;//三选一事件
            }

            //if(@event.data.race_start_info != null)
            isRacing = true;
            for (var i = 0; i < 5; i++)
            {
                isRacing &= (@event.data.home_info.command_info_array[i].is_enable == 0);
            }

            islegal = true;
            //Console.WriteLine("测试用，看到这个说明发送成功\n");
            umaId = @event.data.chara_info.card_id;
            umaStar = @event.data.chara_info.rarity;
            //turn
            var turnNum = @event.data.chara_info.turn;//游戏里回合数从1开始
            turn = turnNum - 1;//ai里回合数从0开始
            vital = @event.data.chara_info.vital;
            maxVital = @event.data.chara_info.max_vital;
            motivation = @event.data.chara_info.motivation;
            friend_qingre = false;
            friend_qingreTurn = 0;

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

            failureRateBias = 0;
            foreach (var effect in @event.data.chara_info.chara_effect_id_array)
            {
                switch (effect)
                {
                    case 6:
                        failureRateBias = 2; break;
                    case 10:
                        failureRateBias = -2; break;
                    case 7:
                        isQieZhe = true; break;
                    case 8:
                        isAiJiao = true; break;
                    case 25:
                        isPositiveThinking = true; break;
                    case 32:
                        isRefreshMind = true; break;
                    case 104:
                        friend_qingre = true; break;
                }
            }

            //统计连续情热了多少回合
            if (@event.data.chara_info.chara_effect_id_array.Any(x => x == 104))
            {
                //统计一下女神情热持续了几回合
                var continuousTurnNum = 0;
                for (var i = turn; i >= 1; i--)
                {
                    if (GameStats.stats[i] == null || !GameStats.stats[i].legend_isEffect104)
                        break;
                    continuousTurnNum++;
                }
                friend_qingreTurn = continuousTurnNum;
            }


            skillPt = 0;
            try
            {
                var ptScoreRate = isQieZhe ? 2.2 : 2.0;
                var ptScore = AiUtils.calculateSkillScore(@event, ptScoreRate);
                skillPt = (int)(ptScore / ptScoreRate);
                ptScoreRate = 2.0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("获取当前技能分失败" + ex.Message);
                skillPt = @event.data.chara_info.skill_point;
            }

            skillScore = 0;
            cardId = new int[6];

            zhongMaBlueCount = new int[5];
            //用属性上限猜蓝因子个数
            {
                var defaultLimit = GameGlobal.FiveStatusLimit[@event.data.chara_info.scenario_id];
                double factor = 16;//每个三星因子可以提多少上限
                if (turn >= 54)//第二次继承结束
                    factor = 22;
                else if (turn >= 30)//第二次继承结束
                    factor = 19;
                for (var i = 0; i < 5; i++)
                {
                    var div = (defaultLimit[i] >= 1200 ? 2 : 1);
                    var threeStarCount = (int)Math.Round((fiveStatusLimit[i] - defaultLimit[i]) / div / factor);
                    if (threeStarCount > 6) threeStarCount = 6;
                    if (threeStarCount < 0) threeStarCount = 0;
                    zhongMaBlueCount[i] = threeStarCount * 3;
                }
            }

            trainLevelCount = new int[5] { 0, 0, 0, 0, 0 };

            var trainLevelClickNumEvery = 4;
            var turnStat = GameStats.stats[@event.data.chara_info.turn];
            if (turnStat == null)
            {
                AnsiConsole.MarkupLine($"[yellow]获取训练等级信息出错[/]");
                for (var i = 0; i < 5; i++)
                {
                    var trId = @event.IsScenario(ScenarioType.Mecha) ? GameGlobal.TrainIdsMecha[i] :
                        GameGlobal.TrainIds[i];
                    var trLevel = @event.data.chara_info.training_level_info_array.First(x => x.command_id == trId).level;
                    var count = (trLevel - 1) * trainLevelClickNumEvery;
                    trainLevelCount[i] = count;
                }
            }
            else
            {
                for (var i = 0; i < 5; i++)
                    trainLevelCount[i] = turnStat.trainLevelCount[i] + trainLevelClickNumEvery * (turnStat.trainLevel[i] - 1);
            }

            //从游戏json的id到ai的人头编号的换算
            foreach (var s in @event.data.chara_info.support_card_array)
            {
                var p = s.position - 1;
                //突破数+10*卡原来的id，例如神团是30137，满破神团就是301374
                cardId[p] = s.limit_break_count + s.support_card_id * 10;
            }

            persons = new PersonBase[6];
            for (var i = 0; i < 6; i++)
                persons[i] = new PersonBase();

            friend_type = 0;
            friend_personId = -1;
            for (var i = 0; i < 6; i++)
            {
                var personJson = @event.data.chara_info.evaluation_info_array.First(x => x.target_id == i + 1);
                persons[i].cardRecord = 0;
                persons[i].friendship = personJson.evaluation;
                switch (cardId[i] / 10)
                {
                    case 30207://ssr 理事长
                        persons[i].personType = 6;
                        //friend_personId = i;
                        //friend_type = 1;
                        break;
                    case 10109://r 理事长
                        persons[i].personType = 6;
                        //friend_personId = i;
                        //friend_type = 1;
                        break;
                    case 30188://ssr 凉花
                        persons[i].personType = 6;
                        //friend_personId = i;
                        //friend_type = 2;
                        break;
                    case 10104://r 凉花
                        persons[i].personType = 6;
                        //friend_personId = i;
                        //friend_type = 2;
                        break;
                    case 30241://团卡
                        persons[i].personType = 1;
                        friend_personId = i;
                        friend_type = 1;
                        break;
                    default:
                        persons[i].personType = 2;
                        break;
                }
            }
            friendship_noncard_yayoi = @event.data.chara_info.evaluation_info_array.Any(x => x.target_id == 102) ?
                @event.data.chara_info.evaluation_info_array.First(x => x.target_id == 102).evaluation : 0;
            friendship_noncard_reporter = @event.data.chara_info.evaluation_info_array.Any(x => x.target_id == 103) ?
                @event.data.chara_info.evaluation_info_array.First(x => x.target_id == 103).evaluation : 0;

            personDistribution = new int[5, 5];
            for (var i = 0; i < 5; i++)
                for (var j = 0; j < 5; j++)
                    personDistribution[i, j] = -1;

            foreach (var train in @event.data.home_info.command_info_array)
            {
                //Console.WriteLine(train.command_id);
                if (!GameGlobal.ToTrainIndex.ContainsKey(train.command_id))//不是正常训练
                    continue;
                //Console.WriteLine("!");
                var trainId = GameGlobal.ToTrainIndex[train.command_id];

                var j = 0;
                foreach (var p in train.training_partner_array)
                {
                    var personIdUmaAi = p == 102 ? 6 : p == 103 ? 7 : p >= 1000 ? 
                        GameGlobal.ToTrainIndex[Database.Names.GetRSupportCardTypeByCharaId(p)] + 10 //npc
                        : p - 1; //支援卡
                    personDistribution[trainId, j] = personIdUmaAi;
                    j += 1;
                }
                foreach (var p in train.tips_event_partner_array)
                {

                    persons[p - 1].isHint = true;
                }
            }

            //计算Lockedtrainid
            {
                var istrainlocked = false;
                var enableidx = -1;
                var command = @event.data.home_info.command_info_array;
                foreach (var train in @event.data.home_info.command_info_array)
                {
                    if (!GameGlobal.ToTrainIndex.ContainsKey(train.command_id))//不是正常训练
                        continue;
                    if (train.is_enable != 1)
                    {
                        istrainlocked = true;
                    }
                    else
                    {
                        enableidx = Convert.ToInt32(train.command_id) % 10;
                    }
                }

                if (istrainlocked)
                {
                    lockedTrainingId = enableidx;
                }
                else
                {
                    lockedTrainingId = -1;
                }
            }

            friend_stage = 0;
            friend_outgoingUsed = new bool[5] { false, false, false, false, false };
            //友人出行用了几次
            if (friend_type != 0)
            {
                var friendJson = @event.data.chara_info.evaluation_info_array.First(x => x.target_id == friend_personId + 1);
                if (friendJson.is_outing == 1)
                {
                    friend_stage = 2;
                    friend_outgoingUsed[0] = friendJson.group_outing_info_array.First(x => x.chara_id == 9047).story_step > 0;
                    friend_outgoingUsed[1] = friendJson.group_outing_info_array.First(x => x.chara_id == 9046).story_step > 0;
                    friend_outgoingUsed[2] = friendJson.group_outing_info_array.First(x => x.chara_id == 9048).story_step > 0;
                    friend_outgoingUsed[3] = friendJson.story_step > 0;
                    friend_outgoingUsed[4] = friendJson.story_step > 1;
                }
                else
                {
                    var friendClicked = false;//友人卡是否点过第一次
                    for (var t = @event.data.chara_info.turn - 1; t >= 1; t--)
                    {
                        if (GameStats.stats[t] == null)
                        {
                            break;
                        }

                        if (!GameGlobal.TrainIds.Any(x => x == GameStats.stats[t].playerChoice)) //没训练
                            continue;
                        if (GameStats.stats[t].isTrainingFailed)//训练失败
                            continue;
                        if (!GameStats.stats[t].legend_friendAtTrain[GameGlobal.ToTrainIndex[GameStats.stats[t].playerChoice]])
                            continue;//没点友人

                        friendClicked = true;
                        break;
                    }
                    if (friendClicked) friend_stage = 1;
                    else friend_stage = 0;
                }

            }
            else
            {
                //friend_outgoingUsed = 0;
            }



            if (!islegal) return;
            var x = @event;
            var lg = @event.data.legend_data_set;



            if (lg != null)
            {
                lg_mainColor =
                 lg.masterly_bonus_info.info_9046 != null ? 0 :
                 lg.masterly_bonus_info.info_9047 != null ? 1 :
                 lg.masterly_bonus_info.info_9048 != null ? 2 : -1;

                lg_gauge = new int[3];
                for(int i = 0; i < 3; i++)
                {
                    lg_gauge[i] = lg.gauge_count_array.First(x => x.legend_id == 9046 + i).count;
                }

                lg_trainingColor = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };

                foreach (var i in lg.command_info_array)
                {
                    int c = i.legend_id - 9046;

                    int training = i.command_type == 1 ? GameGlobal.ToTrainIndex[i.command_id] :
                        i.command_type == 7 ? 5 :
                        i.command_type == 3 ? 6 :
                        i.command_type == 4 ? 7 : -1;
                    if (training >= 0)
                    {
                        lg_trainingColor[training] = c;
                    }
                }

                lg_buffs = new ScenarioBuffInfo_Legend[10]; 
                lg_haveBuff = new bool[57];
                for (int i = 0; i < 10; i++)
                    lg_buffs[i] = new ScenarioBuffInfo_Legend();
                for (int i = 0; i < 57; i++)
                    lg_haveBuff[i] = false;
                int buffIdx = 0;
                foreach (var buff in lg.buff_info_array)
                {
                    int shortId = GameGlobal.LegendBuffShortId[buff.buff_id];
                    lg_haveBuff[shortId] = true;
                    lg_buffs[buffIdx].buffId = shortId;
                    lg_buffs[buffIdx].coolTime=buff.cool_time;
                    lg_buffs[buffIdx].isActive = buff.is_active > 0;
                    buffIdx += 1;
                }


                lg_pickedBuffsNum = lg.obtainable_buff_id_array.Length;
                lg_pickedBuffs = new int[9];
                for (int i = 0; i < 9; i++)
                    lg_pickedBuffs[i] = -1;
                for (int i = 0; i < lg_pickedBuffsNum; i++)
                    lg_pickedBuffs[i] = GameGlobal.LegendBuffShortId[lg.obtainable_buff_id_array[i]];

                lg_blue_active = false;
                lg_blue_remainCount = 0;
                lg_blue_currentStepCount = 0;
                lg_blue_canExtendCount = 0;
                lg_green_active = false;
                lg_green_continuationZoneCount = 0;
                lg_green_currentStepCount = 0;
                lg_red_friendsGauge = new int[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                lg_red_friendsLv = new int[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };


                if (lg_mainColor == 0)
                {
                    lg_blue_active = lg.masterly_bonus_info.info_9046.remain_count > 0;
                    lg_blue_remainCount = lg.masterly_bonus_info.info_9046.remain_count;
                    lg_blue_currentStepCount = lg.masterly_bonus_info.info_9046.current_step_count;
                    lg_blue_canExtendCount = lg.masterly_bonus_info.info_9046.can_extend_count;
                }
                else if (lg_mainColor == 1)
                {
                    lg_green_active= lg.masterly_bonus_info.info_9047.continuation_zone_count > 0;
                    lg_green_continuationZoneCount = lg.masterly_bonus_info.info_9047.continuation_zone_count;
                    lg_green_currentStepCount = lg.masterly_bonus_info.info_9047.current_step_count;
                }
                else if (lg_mainColor == 2)
                {
                    //判断npc是哪个属性。根据r卡训练来查找，最多允许一个查不到
                    var npcIdToTrain=new Dictionary<int, int>();
                    foreach (var f in lg.masterly_bonus_info.info_9048.friend_gauge_array)
                    {
                        if(f.training_partner_id > 1000)
                        {
                            var c = Database.Names.GetRSupportCardTypeByCharaId(f.training_partner_id);
                            c = c < 0 ? -1 : GameGlobal.ToTrainIndex[c];//未知是-1

                            npcIdToTrain.Add(f.training_partner_id,c);

                        }
                    }
                    if(npcIdToTrain.Count != 5)
                    {
                        throw new Exception($"NPC不是5个");
                    }
                    var unknownCount = npcIdToTrain.Count(x => x.Value < 0);
                    if (unknownCount >= 2)
                    {
                        AnsiConsole.MarkupLine("[red]不少于2个NPC无法获取属性，无法运行ai，请及时更新数据[/]");
                        islegal=false;
                        return;
                    }

                    if (unknownCount == 1)
                    {
                        var unknownId = npcIdToTrain.First(x => x.Value < 0).Key;
                        for (var i = 0; i < 5; i++)
                        {
                            if (npcIdToTrain.Count(x => x.Value == i) == 0)
                            {
                                npcIdToTrain[unknownId] = i;
                                if (turn == 36)
                                {
                                    AnsiConsole.MarkupLine($"[yellow]有1个NPC(id={unknownId})无法获取属性，已推断为{GameGlobal.TrainNamesSimple[GameGlobal.TrainIds[i]]}，建议及时更新数据[/]");
                                }
                            }
                        }
                    }

                    foreach (var f in lg.masterly_bonus_info.info_9048.friend_gauge_array)
                    {
                        var pid = f.training_partner_id <= 6 ? f.training_partner_id - 1 : npcIdToTrain[f.training_partner_id] + 10;
                        lg_red_friendsGauge[pid] = f.gauge_value;
                        lg_red_friendsLv[pid] = f.level;
                    }
                }
            }
            else islegal = false;
        }
        public void doSend()
        {
            if (this.islegal == false)
            {
                return;
            }
            var wsSubscribeCount = SubscribeAiInfo.Signal(this);
            if (wsSubscribeCount > 0)
                AnsiConsole.MarkupLine("\n[aqua]AI计算中...[/]");

            var currentGSdirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmamusumeResponseAnalyzer", "GameData");
            Directory.CreateDirectory(currentGSdirectory);
            var success = false;
            var tried = 0;
            do
            {
                try
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }; // 去掉空值避免C++端抽风
                    File.WriteAllText($@"{currentGSdirectory}/thisTurn.json", JsonConvert.SerializeObject(this, Formatting.Indented, settings));
                    File.WriteAllText($@"{currentGSdirectory}/turn{this.turn}.json", JsonConvert.SerializeObject(this, Formatting.Indented, settings));
                    success = true; // 写入成功，跳出循环
                    break;
                }
                catch
                {
                    tried++;
                    AnsiConsole.MarkupLine("[yellow]写入失败[/]");
                }
            } while (!success && tried < 10);
            if (!success)
            {
                AnsiConsole.MarkupLine($@"[red]写入{currentGSdirectory}/thisTurn.json失败！[/]");
            }
        }
    }
}
