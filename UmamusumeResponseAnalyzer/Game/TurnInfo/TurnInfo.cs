﻿using Gallop;
using System.Collections.Frozen;
using UmamusumeResponseAnalyzer.Entities;

namespace UmamusumeResponseAnalyzer.Game.TurnInfo
{
    public class TurnInfo(SingleModeCheckEventResponse.CommonResponse resp)
    {
        private SingleModeChara CharaInfo => resp.chara_info;
        /// <summary>
        /// 马娘ID
        /// </summary>
        public int CharacterId => int.Parse(CharaInfo.card_id.ToString()[..4]);
        public int SpeedRevised => ScoreUtils.ReviseOver1200(CharaInfo.speed);
        public int StaminaRevised => ScoreUtils.ReviseOver1200(CharaInfo.stamina);
        public int PowerRevised => ScoreUtils.ReviseOver1200(CharaInfo.power);
        public int GutsRevised => ScoreUtils.ReviseOver1200(CharaInfo.guts);
        public int WizRevised => ScoreUtils.ReviseOver1200(CharaInfo.wiz);
        public int[] Stats => [CharaInfo.speed, CharaInfo.stamina, CharaInfo.power, CharaInfo.guts, CharaInfo.wiz];
        public int[] StatsRevised => [SpeedRevised, StaminaRevised, PowerRevised, GutsRevised, WizRevised];
        public int[] MaxStatsRevised => [
            ScoreUtils.ReviseOver1200(CharaInfo.max_speed),
            ScoreUtils.ReviseOver1200(CharaInfo.max_stamina),
            ScoreUtils.ReviseOver1200(CharaInfo.max_power),
            ScoreUtils.ReviseOver1200(CharaInfo.max_guts),
            ScoreUtils.ReviseOver1200(CharaInfo.max_wiz)]
        ;
        public ScenarioType Scenario => (ScenarioType)CharaInfo.scenario_id;
        public int Turn => CharaInfo.turn;
        public int TotalStats => StatsRevised.Sum();
        public int Year => (Turn - 1) / 24 + 1;
        public Motivation Motivation => new(CharaInfo.motivation);
        public int Vital => CharaInfo.vital;
        public int MaxVital => CharaInfo.max_vital;
        /// <summary>
        /// Key是位置，Value是support_card_id
        /// </summary>
        public FrozenDictionary<int, int> SupportCards => CharaInfo.support_card_array.ToDictionary(x => x.position, x => x.support_card_id).ToFrozenDictionary();
        public FrozenDictionary<int, EvaluationInfo> Evaluations => CharaInfo.evaluation_info_array.ToDictionary(x => x.target_id, x => x).ToFrozenDictionary();
        public int Month => ((Turn - 1) % 24) / 2 + 1;
        public string HalfMonth => (Turn % 2 == 0) ? "后半" : "前半";
        public int TotalTurns = 78;
        public bool IsFreeContinueAvailable => resp.home_info.free_continue_time < DateTimeOffset.Now.ToUnixTimeSeconds();
        public bool IsGoldenSuccession => resp.unchecked_event_array.Any(x => x.succession_event_info.effect_type == 2);

        public bool IsScenario<T>(ScenarioType type, out T turnInfo) where T : TurnInfo
        {
            turnInfo = (T)typeof(T).GetConstructor([resp.GetType()])?.Invoke([resp])!;
            return type == Scenario;
        }
        public bool IsScenario(ScenarioType type) => IsScenario<TurnInfo>(type, out _);
        public SingleModeCheckEventResponse.CommonResponse GetCommonResponse() => resp;
    }
}