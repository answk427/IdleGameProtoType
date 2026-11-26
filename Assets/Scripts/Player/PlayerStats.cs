using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum UpgradeStatType
{
    Hp,
    Attack,
    AttackSpeed
}

public enum LevelUpResult
{
    Success,
    NotEnoughExp,
    MaxLevel,
}

public enum LearnSkillResult
{
    Success,
    AlreadyLearned,
    UnknownSkill,
    LevelTooLow,
}

public enum SkillSlotResult
{
    Success,
    Unchanged,
    NotLearned,
    InvalidSlot,
}

// 진행도에서 나오는 플레이어의 전투 능력치. 캐릭터(PlayerController)가 전투원에 넣는다.
public readonly struct PlayerCombatStats : IEquatable<PlayerCombatStats>
{
    public readonly GameNumber MaxHp;
    public readonly GameNumber AttackDamage;
    public readonly float AttackInterval;

    public PlayerCombatStats(GameNumber maxHp, GameNumber attackDamage, float attackInterval)
    {
        MaxHp = maxHp;
        AttackDamage = attackDamage;
        AttackInterval = attackInterval;
    }

    public bool Equals(PlayerCombatStats other) =>
        MaxHp == other.MaxHp && AttackDamage == other.AttackDamage && AttackInterval.Equals(other.AttackInterval);
    public override bool Equals(object obj) => obj is PlayerCombatStats other && Equals(other);
    public override int GetHashCode() => MaxHp.GetHashCode() ^ (AttackDamage.GetHashCode() * 31) ^ AttackInterval.GetHashCode();
    public override string ToString() => $"HP {MaxHp} 공격력 {AttackDamage} 간격 {AttackInterval:R}";
}

// 플레이어 진행도(레벨, 경험치, 업그레이드, 스킬)의 주인. 최종 스탯 = 레벨 기본 스탯(PlayerStatDatabase) + 업그레이드 보너스.
// (docs/COMBAT_ARCHITECTURE.md 10-10, docs/UI_DESIGN.md 5-3)
//
// - 상태(레벨, 경험치, 업그레이드 단계, 배운 스킬, 장착 슬롯)와 파생 상태(필요 경험치, 레벨업 가능, 최대 레벨, 전투 능력치)는 반응형이다.
//   구독하는 순간 지금 값을 받고, 바뀌면 따라온다. 진행도를 통째로 바꿔도(LoadSave) 값만 덮어쓰면 화면과 캐릭터가 따라온다.
// - 사건(레벨업, 업그레이드, 스킬 학습·장착)은 저장 계기다. 명령이 상태를 다 바꾼 뒤 마지막에 낸다. LoadSave는 내지 않는다.
// - 규칙: 저장 데이터(DTO)를 먼저 바꾸고 상태를 알린다. 파생 상태는 DTO에서 계산하므로 중간 조합이 바깥에 보이지 않고,
//   알림을 받은 쪽이 바로 저장해도 새 값이 나간다.
public sealed class PlayerStats
{
    // ── 참조 ────────────────────────────────────────────────
    private PlayerStatDatabase statData;
    private PlayerUpgradeConfig upgradeConfig;
    private PlayerStatsSaveData saveData = new PlayerStatsSaveData();

    // 배울 수 있는 스킬 전부(스킬 표). 학습 조건과 "배울 스킬 있음"이 이것을 본다.
    private IReadOnlyList<SkillEntry> skills = Array.Empty<SkillEntry>();
    private readonly Dictionary<int, SkillEntry> skillsById = new Dictionary<int, SkillEntry>();

    // 스탯 종류별 동작(IUpgradeStat)을 한 곳에 등록. 새 스탯은 IUpgradeStat 구현 클래스 하나를 만들어 여기에 등록한다.
    private Dictionary<UpgradeStatType, IUpgradeStat> upgradeStats;

    // ── 상태 ────────────────────────────────────────────────
    private readonly ReactiveProperty<int> level = new ReactiveProperty<int>(1);
    private readonly ReactiveProperty<int> exp = new ReactiveProperty<int>(0);
    private readonly Dictionary<UpgradeStatType, ReactiveProperty<int>> upgradeLevels = new Dictionary<UpgradeStatType, ReactiveProperty<int>>();

    // 모음은 내용을 고치지 않고 새 목록으로 교체한다. 바꾸는 곳은 SetLearnedSkills/SetEquippedSlots뿐이다.
    private readonly ReactiveProperty<IReadOnlyList<int>> learnedSkills =
        new ReactiveProperty<IReadOnlyList<int>>(Array.Empty<int>());
    private readonly ReactiveProperty<IReadOnlyList<int>> equippedSlots =
        new ReactiveProperty<IReadOnlyList<int>>(Array.AsReadOnly(new PlayerStatsSaveData().equippedSkillSlotIds));

    // ── 파생 상태(Initialize에서 만든다) ─────────────────────
    private DerivedProperty<int> requiredExp;
    private DerivedProperty<bool> isMaxLevel;
    private DerivedProperty<bool> canLevelUp;
    private DerivedProperty<PlayerCombatStats> combatStats;
    private DerivedProperty<bool> hasLearnableSkill;

    // ── 사건(저장 계기) ─────────────────────────────────────
    private readonly Subject<int> levelReached = new Subject<int>();
    private readonly Subject<UpgradeStatType> upgraded = new Subject<UpgradeStatType>();
    private readonly Subject<int> skillLearned = new Subject<int>();
    private readonly Subject<int> skillEquipChanged = new Subject<int>();

    public IReadOnlyReactiveProperty<int> Level => level;
    public IReadOnlyReactiveProperty<int> Exp => exp;
    public IReadOnlyReactiveProperty<int> UpgradeLevel(UpgradeStatType type) => upgradeLevels[type];
    public IReadOnlyReactiveProperty<IReadOnlyList<int>> LearnedSkills => learnedSkills;
    public IReadOnlyReactiveProperty<IReadOnlyList<int>> EquippedSlots => equippedSlots;

    public IReadOnlyReactiveProperty<int> RequiredExp => requiredExp;
    public IReadOnlyReactiveProperty<bool> IsMaxLevel => isMaxLevel;
    // 레벨업은 자동이 아니다. 경험치는 쌓이고, 캐릭터 탭의 LEVEL UP 버튼이 TryLevelUp을 부른다.
    public IReadOnlyReactiveProperty<bool> CanLevelUp => canLevelUp;
    public IReadOnlyReactiveProperty<PlayerCombatStats> CombatStats => combatStats;
    // 지금 레벨로 배울 수 있는데 아직 배우지 않은 스킬이 있다(레드닷 조건, docs/UI_DESIGN.md 5-8).
    public IReadOnlyReactiveProperty<bool> HasLearnableSkill => hasLearnableSkill;

    // 레벨업한 뒤의 레벨(여러 레벨을 한 번에 올리면 끝 레벨로 한 번).
    public Observable<int> LevelReached => levelReached;
    public Observable<UpgradeStatType> Upgraded => upgraded;
    public Observable<int> SkillLearned => skillLearned;       // 스킬 id
    public Observable<int> SkillEquipChanged => skillEquipChanged; // 명령이 바꾼 슬롯 번호

    public int MaxLevel => statData != null ? statData.MaxLevel : 1;

    // ── 초기화 ──────────────────────────────────────────────

    // 이동속도처럼 저장되지 않는 캐릭터 속성은 여기(진행도)가 아니라 캐릭터가 갖는다.
    // skills: 스킬 표(GameInitializer가 GameDatabaseManager에서 넘긴다). 없으면 배울 스킬이 없다.
    public void Initialize(PlayerStatDatabase statData, PlayerUpgradeConfig upgradeConfig, IReadOnlyList<SkillEntry> skills = null)
    {
        if (upgradeStats != null) throw new InvalidOperationException("PlayerStats는 한 번만 초기화한다.");
        this.statData = statData;
        this.upgradeConfig = upgradeConfig;

        this.skills = skills ?? Array.Empty<SkillEntry>();
        foreach (SkillEntry entry in this.skills)
        {
            if (entry?.data != null) skillsById[entry.data.Id] = entry;
        }

        IUpgradeStat[] stats =
        {
            new HpUpgradeStat(this, upgradeConfig),
            new AttackUpgradeStat(this, upgradeConfig),
            new AttackSpeedUpgradeStat(this, upgradeConfig),
        };

        upgradeStats = new Dictionary<UpgradeStatType, IUpgradeStat>();
        foreach (IUpgradeStat stat in stats)
        {
            upgradeStats[stat.Type] = stat;
            upgradeLevels[stat.Type] = new ReactiveProperty<int>(0);
        }

        requiredExp = new DerivedProperty<int>(() => statData != null ? statData.GetRequiredExp(saveData.level) : 0, level);
        isMaxLevel = new DerivedProperty<bool>(() => saveData.level >= MaxLevel, level);
        canLevelUp = new DerivedProperty<bool>(
            () => saveData.level < MaxLevel && saveData.currentExp >= RequiredExpOf(saveData.level), level, exp);

        var sources = new List<IChangeSignal> { level };
        sources.AddRange(upgradeLevels.Values);
        combatStats = new DerivedProperty<PlayerCombatStats>(ComputeCombatStats, sources.ToArray());
        hasLearnableSkill = new DerivedProperty<bool>(ComputeHasLearnableSkill, level, learnedSkills);

        LoadSave(null); // 기본값
    }

    // 에디터의 튜닝 덮어쓰기처럼 데이터(레벨 표, 업그레이드 설정, 스킬 표)가 바뀌면 파생 상태를 다시 계산한다.
    public void RecomputeFromData()
    {
        requiredExp.Recompute();
        isMaxLevel.Recompute();
        canLevelUp.Recompute();
        combatStats.Recompute();
        hasLearnableSkill.Recompute();
    }

    // GameSession.Apply()가 호출한다. 객체를 새로 만들지 않고 값만 덮어쓴다(참조하는 쪽이 다시 연결할 필요가 없다).
    // 실행 중에도 불릴 수 있다(진행도 교체, 서버 롤백). 상태가 바뀌므로 구독하는 쪽이 따라온다. 저장 계기 사건은 내지 않는다.
    public void LoadSave(PlayerStatsSaveData data)
    {
        saveData = data ?? new PlayerStatsSaveData();
        saveData.learnedSkillIds ??= new List<int>();
        saveData.equippedSkillSlotIds ??= new PlayerStatsSaveData().equippedSkillSlotIds;

        // DTO가 다 정해진 뒤에 알린다. 파생 상태는 첫 알림에서 이미 최종 값이 된다.
        level.Value = saveData.level;
        exp.Value = saveData.currentExp;
        foreach (KeyValuePair<UpgradeStatType, IUpgradeStat> pair in upgradeStats)
            upgradeLevels[pair.Key].Value = pair.Value.GetLevel();
        SetLearnedSkills(saveData.learnedSkillIds);
        SetEquippedSlots(saveData.equippedSkillSlotIds);
    }

    // ── 경험치 / 레벨업 ─────────────────────────────────────
    // 경험치는 쌓이기만 하고, 레벨업은 TryLevelUp()을 명시적으로 불러야 일어난다(캐릭터 탭의 LEVEL UP 버튼).
    // 경험치 획득은 저장 계기가 아니다(처치마다 저장하면 잦다). 앱을 내리거나 끌 때, 다른 계기에서 같이 저장된다.

    public void AddExp(int amount)
    {
        if (amount <= 0 || saveData.level >= MaxLevel) return;

        saveData.currentExp += amount;
        exp.Value = saveData.currentExp;
    }

    // 밀린 경험치가 여러 레벨 분량이면 한 번에 다 올린다. 상태와 사건은 끝에서 한 번씩 알린다.
    // 실패하면 이유를 돌려준다(UI가 이유별로 안내한다, docs/UI_DESIGN.md 10-1).
    public LevelUpResult TryLevelUp()
    {
        if (saveData.level >= MaxLevel) return LevelUpResult.MaxLevel;
        if (!canLevelUp.Value) return LevelUpResult.NotEnoughExp;

        while (saveData.level < MaxLevel && saveData.currentExp >= RequiredExpOf(saveData.level))
        {
            saveData.currentExp -= RequiredExpOf(saveData.level);
            saveData.level++;
        }

        level.Value = saveData.level;
        exp.Value = saveData.currentExp;
        Debug.Log($"[PlayerStats] 레벨업 → Lv.{saveData.level} | {combatStats.Value}");
        levelReached.OnNext(saveData.level);
        return LevelUpResult.Success;
    }

    // ── 업그레이드 (돈은 GameSession.TryUpgrade가 검사) ─────

    public GameNumber GetNextUpgradeCost(UpgradeStatType type)
    {
        IUpgradeStat stat = upgradeStats[type];
        return stat.GetCost(stat.GetLevel());
    }

    public bool IsUpgradeMaxed(UpgradeStatType type)
    {
        if (upgradeConfig.maxUpgradeLevel <= 0) return false;
        return upgradeStats[type].GetLevel() >= upgradeConfig.maxUpgradeLevel;
    }

    // 이 업그레이드를 한 단계 더 했을 때의 전투 능력치(캐릭터 탭의 "현재 → 다음" 표시용).
    // 실제 능력치(CombatStats)와 같은 계산을 쓰므로 공식이 바뀌어도 미리보기가 따로 틀리지 않는다.
    // 최대 단계여도 한 단계 위를 계산한다(살 수 있는지는 IsUpgradeMaxed가 정한다).
    public PlayerCombatStats PreviewUpgrade(UpgradeStatType type) => ComputeCombatStats(type);

    public void Upgrade(UpgradeStatType type)
    {
        if (IsUpgradeMaxed(type))
        {
            Debug.LogWarning($"[PlayerStats] {type} 최대 업그레이드 도달");
            return;
        }

        IUpgradeStat stat = upgradeStats[type];
        stat.IncrementLevel();
        upgradeLevels[type].Value = stat.GetLevel();

        Debug.Log($"[PlayerStats] {type} 업그레이드 → Lv.{stat.GetLevel()} | {combatStats.Value}");
        upgraded.OnNext(type);
    }

    // ── 스킬 학습 / 장착 ────────────────────────────────────
    // 학습 조건은 플레이어 레벨만 체크. 명령은 동기 + 실패 이유 반환. 상태를 다 바꾼 뒤에 사건을 낸다.

    public bool IsSkillLearned(int skillId) => learnedSkills.Value.Contains(skillId);

    public bool CanLearnSkill(int skillId) => CheckLearnSkill(skillId) == LearnSkillResult.Success;

    public LearnSkillResult LearnSkill(int skillId)
    {
        LearnSkillResult result = CheckLearnSkill(skillId);
        if (result != LearnSkillResult.Success) return result;

        SetLearnedSkills(learnedSkills.Value.Append(skillId));
        Debug.Log($"[PlayerStats] 스킬 학습: {skillId}");
        skillLearned.OnNext(skillId);
        return LearnSkillResult.Success;
    }

    public int GetEquippedSkillId(int slotIndex)
    {
        IReadOnlyList<int> slots = equippedSlots.Value;
        if (slotIndex < 0 || slotIndex >= slots.Count) return PlayerStatsSaveData.EmptySlot;
        return slots[slotIndex];
    }

    public SkillSlotResult EquipSkill(int skillId, int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return SkillSlotResult.InvalidSlot;
        if (!IsSkillLearned(skillId)) return SkillSlotResult.NotLearned;

        // 새 목록은 내용이 같아도 "다른 값"으로 알려지므로, 바뀌지 않는 명령은 여기서 거른다.
        if (equippedSlots.Value[slotIndex] == skillId) return SkillSlotResult.Unchanged;

        int[] slots = equippedSlots.Value.ToArray();

        // 다른 슬롯에 이미 배치되어 있던 스킬이면 그 슬롯은 비운다 (중복 배치 방지)
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == skillId) slots[i] = PlayerStatsSaveData.EmptySlot;
        }

        slots[slotIndex] = skillId;
        SetEquippedSlots(slots);
        skillEquipChanged.OnNext(slotIndex);
        return SkillSlotResult.Success;
    }

    public SkillSlotResult UnequipSkill(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return SkillSlotResult.InvalidSlot;
        if (equippedSlots.Value[slotIndex] == PlayerStatsSaveData.EmptySlot) return SkillSlotResult.Unchanged;

        int[] slots = equippedSlots.Value.ToArray();
        slots[slotIndex] = PlayerStatsSaveData.EmptySlot;
        SetEquippedSlots(slots);
        skillEquipChanged.OnNext(slotIndex);
        return SkillSlotResult.Success;
    }

    private LearnSkillResult CheckLearnSkill(int skillId)
    {
        if (IsSkillLearned(skillId)) return LearnSkillResult.AlreadyLearned;
        if (!skillsById.TryGetValue(skillId, out SkillEntry entry)) return LearnSkillResult.UnknownSkill;

        return saveData.level >= entry.data.RequiredLevel ? LearnSkillResult.Success : LearnSkillResult.LevelTooLow;
    }

    // 학습 조건(CheckLearnSkill)과 같은 규칙을 저장 데이터에서 계산한다(진행도 교체 도중의 조합이 보이지 않게).
    private bool ComputeHasLearnableSkill()
    {
        List<int> learned = saveData.learnedSkillIds; // LoadSave 전(기본값)에는 null일 수 있다
        foreach (SkillEntry entry in skills)
        {
            if (entry?.data == null) continue;
            if (saveData.level >= entry.data.RequiredLevel && (learned == null || !learned.Contains(entry.data.Id))) return true;
        }
        return false;
    }

    private bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < equippedSlots.Value.Count;

    // DTO를 먼저 맞추고 상태를 알린다. 알림을 받은 쪽이 바로 저장해도 새 값이 나간다.
    private void SetLearnedSkills(IEnumerable<int> skillIds)
    {
        int[] next = skillIds.ToArray();
        saveData.learnedSkillIds = new List<int>(next);
        learnedSkills.Value = Array.AsReadOnly(next);
    }

    private void SetEquippedSlots(IEnumerable<int> skillIds)
    {
        int[] next = skillIds.ToArray();
        saveData.equippedSkillSlotIds = (int[])next.Clone();
        equippedSlots.Value = Array.AsReadOnly(next);
    }

    // ── 계산(파생 상태와 업그레이드 표시가 쓴다) ─────────────

    private int RequiredExpOf(int atLevel) => statData != null ? statData.GetRequiredExp(atLevel) : 0;

    // HP·공격력은 전투 수치라 GameNumber다(D14, 계산은 double이라 int처럼 넘치지 않는다).
    private PlayerCombatStats ComputeCombatStats() => ComputeCombatStats(null);

    // plusOne: 그 업그레이드만 한 단계 더 한 것으로 계산한다(미리보기). 실제 능력치와 미리보기가 이 한 함수를 쓴다.
    // 단계는 long으로 센다(int 최대 단계에서 +1이 넘치지 않게). 공격 간격은 예전과 같은 float 계산(정수 × float)이라
    // 전투 시각이 비트 단위로 그대로다.
    private PlayerCombatStats ComputeCombatStats(UpgradeStatType? plusOne)
    {
        long LevelOf(UpgradeStatType type) => (long)upgradeStats[type].GetLevel() + (plusOne == type ? 1 : 0);

        PlayerStatData baseStat = statData.GetByLevel(saveData.level);
        return new PlayerCombatStats(
            GameNumber.FromDouble((double)baseStat.BaseMaxHp + (double)LevelOf(UpgradeStatType.Hp) * upgradeConfig.settings.hpPerUpgrade),
            GameNumber.FromDouble((double)baseStat.BaseAttackDamage + (double)LevelOf(UpgradeStatType.Attack) * upgradeConfig.settings.attackPerUpgrade),
            Mathf.Max(0.1f, baseStat.BaseAttackInterval - LevelOf(UpgradeStatType.AttackSpeed) * upgradeConfig.settings.attackSpeedPerUpgrade));
    }

    // 스킬 목록/슬롯은 이 객체로 고치지 말 것(상태에 알려지지 않는다). 위 명령을 쓴다.
    public PlayerStatsSaveData GetSaveData() => saveData;
}
