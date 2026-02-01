using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests pour le StatusEffectManager.
/// </summary>
[TestFixture]
public class StatusEffectManagerTests
{
    private GameObject _entityObj;
    private StatusEffectManager _manager;

    [SetUp]
    public void SetUp()
    {
        _entityObj = new GameObject("TestEntity");
        _manager = _entityObj.AddComponent<StatusEffectManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_entityObj != null)
        {
            Object.DestroyImmediate(_entityObj);
        }
    }

    [Test]
    public void StatusEffectManager_InitialState_NoEffects()
    {
        Assert.AreEqual(0, _manager.ActiveEffects.Count);
        Assert.AreEqual(0, _manager.BuffCount);
        Assert.AreEqual(0, _manager.DebuffCount);
    }

    [Test]
    public void StatusEffectManager_InitialModifiers_AreZero()
    {
        Assert.AreEqual(0f, _manager.AttackModifier);
        Assert.AreEqual(0f, _manager.DefenseModifier);
        Assert.AreEqual(0f, _manager.SpeedModifier);
        Assert.AreEqual(0f, _manager.CritRateModifier);
        Assert.AreEqual(0f, _manager.CritDamageModifier);
    }

    [Test]
    public void StatusEffectManager_InitialState_NotStunned()
    {
        Assert.IsFalse(_manager.IsStunned);
    }

    [Test]
    public void StatusEffectManager_InitialState_NotSilenced()
    {
        Assert.IsFalse(_manager.IsSilenced);
    }

    [Test]
    public void StatusEffectManager_InitialState_NotInvincible()
    {
        Assert.IsFalse(_manager.IsInvincible);
    }

    [Test]
    public void StatusEffectManager_ApplyEffect_ReturnsNull_WithNullData()
    {
        var result = _manager.ApplyEffect(null);
        Assert.IsNull(result);
    }

    [Test]
    public void StatusEffectManager_ApplyEffect_CreatesInstance()
    {
        var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
        effectData.effectName = "TestBuff";
        effectData.effectType = StatusEffectType.AttackUp;
        effectData.category = StatusEffectCategory.Buff;
        effectData.baseDuration = 10f;
        effectData.value = 0.1f;

        var instance = _manager.ApplyEffect(effectData);

        Assert.IsNotNull(instance);
        Assert.AreEqual(1, _manager.ActiveEffects.Count);
        Assert.AreEqual(1, _manager.BuffCount);

        Object.DestroyImmediate(effectData);
    }

    [Test]
    public void StatusEffectManager_HasEffect_ReturnsFalse_WhenNoEffect()
    {
        Assert.IsFalse(_manager.HasEffect(StatusEffectType.AttackUp));
    }

    [Test]
    public void StatusEffectManager_HasEffect_ReturnsTrue_WhenEffectApplied()
    {
        var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
        effectData.effectType = StatusEffectType.DefenseUp;
        effectData.category = StatusEffectCategory.Buff;
        effectData.baseDuration = 10f;

        _manager.ApplyEffect(effectData);

        Assert.IsTrue(_manager.HasEffect(StatusEffectType.DefenseUp));

        Object.DestroyImmediate(effectData);
    }

    [Test]
    public void StatusEffectManager_RemoveEffect_RemovesExistingEffect()
    {
        var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
        effectData.effectType = StatusEffectType.SpeedUp;
        effectData.category = StatusEffectCategory.Buff;
        effectData.baseDuration = 10f;

        _manager.ApplyEffect(effectData);
        Assert.IsTrue(_manager.HasEffect(StatusEffectType.SpeedUp));

        bool removed = _manager.RemoveEffect(StatusEffectType.SpeedUp);

        Assert.IsTrue(removed);
        Assert.IsFalse(_manager.HasEffect(StatusEffectType.SpeedUp));

        Object.DestroyImmediate(effectData);
    }

    [Test]
    public void StatusEffectManager_RemoveEffect_ReturnsFalse_WhenNoEffect()
    {
        bool removed = _manager.RemoveEffect(StatusEffectType.Stun);
        Assert.IsFalse(removed);
    }

    [Test]
    public void StatusEffectManager_RemoveAllBuffs_RemovesOnlyBuffs()
    {
        var buff = ScriptableObject.CreateInstance<StatusEffectData>();
        buff.effectType = StatusEffectType.AttackUp;
        buff.category = StatusEffectCategory.Buff;
        buff.baseDuration = 10f;

        var debuff = ScriptableObject.CreateInstance<StatusEffectData>();
        debuff.effectType = StatusEffectType.AttackDown;
        debuff.category = StatusEffectCategory.Debuff;
        debuff.baseDuration = 10f;

        _manager.ApplyEffect(buff);
        _manager.ApplyEffect(debuff);

        Assert.AreEqual(1, _manager.BuffCount);
        Assert.AreEqual(1, _manager.DebuffCount);

        _manager.RemoveAllBuffs();

        Assert.AreEqual(0, _manager.BuffCount);
        Assert.AreEqual(1, _manager.DebuffCount);

        Object.DestroyImmediate(buff);
        Object.DestroyImmediate(debuff);
    }

    [Test]
    public void StatusEffectManager_RemoveAllDebuffs_RemovesOnlyDebuffs()
    {
        var buff = ScriptableObject.CreateInstance<StatusEffectData>();
        buff.effectType = StatusEffectType.DefenseUp;
        buff.category = StatusEffectCategory.Buff;
        buff.baseDuration = 10f;

        var debuff = ScriptableObject.CreateInstance<StatusEffectData>();
        debuff.effectType = StatusEffectType.Slow;
        debuff.category = StatusEffectCategory.Debuff;
        debuff.baseDuration = 10f;

        _manager.ApplyEffect(buff);
        _manager.ApplyEffect(debuff);

        _manager.RemoveAllDebuffs();

        Assert.AreEqual(1, _manager.BuffCount);
        Assert.AreEqual(0, _manager.DebuffCount);

        Object.DestroyImmediate(buff);
        Object.DestroyImmediate(debuff);
    }

    [Test]
    public void StatusEffectManager_RemoveAllEffects_RemovesEverything()
    {
        var buff = ScriptableObject.CreateInstance<StatusEffectData>();
        buff.effectType = StatusEffectType.CritRateUp;
        buff.category = StatusEffectCategory.Buff;
        buff.baseDuration = 10f;

        var debuff = ScriptableObject.CreateInstance<StatusEffectData>();
        debuff.effectType = StatusEffectType.Poison;
        debuff.category = StatusEffectCategory.Debuff;
        debuff.baseDuration = 10f;

        _manager.ApplyEffect(buff);
        _manager.ApplyEffect(debuff);

        _manager.RemoveAllEffects();

        Assert.AreEqual(0, _manager.ActiveEffects.Count);

        Object.DestroyImmediate(buff);
        Object.DestroyImmediate(debuff);
    }

    [Test]
    public void StatusEffectManager_GetEffectStacks_ReturnsZero_WhenNoEffect()
    {
        int stacks = _manager.GetEffectStacks(StatusEffectType.Bleed);
        Assert.AreEqual(0, stacks);
    }

    [Test]
    public void StatusEffectManager_ModifyOutgoingDamage_AppliesAttackModifier()
    {
        // Sans modificateur
        float baseDamage = 100f;
        float modified = _manager.ModifyOutgoingDamage(baseDamage);
        Assert.AreEqual(100f, modified);
    }

    [Test]
    public void StatusEffectManager_ModifyIncomingDamage_ReturnsSameDamage_WithNoEffects()
    {
        float baseDamage = 50f;
        float modified = _manager.ModifyIncomingDamage(baseDamage);
        Assert.AreEqual(50f, modified);
    }

    [Test]
    public void StatusEffectManager_FindEffect_ReturnsNull_WhenNoEffect()
    {
        var effect = _manager.FindEffect(StatusEffectType.Freeze);
        Assert.IsNull(effect);
    }

    [Test]
    public void StatusEffectManager_FindEffect_ReturnsEffect_WhenExists()
    {
        var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
        effectData.effectType = StatusEffectType.Regeneration;
        effectData.category = StatusEffectCategory.Buff;
        effectData.baseDuration = 10f;

        _manager.ApplyEffect(effectData);

        var found = _manager.FindEffect(StatusEffectType.Regeneration);
        Assert.IsNotNull(found);

        Object.DestroyImmediate(effectData);
    }
}
