using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de combat de base.
/// </summary>
public class DamageTypeTests
{
    [Test]
    public void DamageType_HasPhysicalType()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Physical));
    }

    [Test]
    public void DamageType_HasAllElementalTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Fire));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Water));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Ice));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Electric));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Wind));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Earth));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Light));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DamageType), DamageType.Dark));
    }
}

/// <summary>
/// Tests pour DamageInfo.
/// </summary>
public class DamageInfoTests
{
    [Test]
    public void DamageInfo_StoresBaseDamage()
    {
        // Arrange & Act
        var info = new DamageInfo(100f, DamageType.Physical);

        // Assert
        Assert.AreEqual(100f, info.baseDamage);
        Assert.AreEqual(DamageType.Physical, info.damageType);
    }

    [Test]
    public void DamageInfo_StoresAttacker()
    {
        // Arrange
        var attacker = new GameObject("Attacker");

        // Act
        var info = new DamageInfo(50f, DamageType.Fire, attacker);

        // Assert
        Assert.AreEqual(attacker, info.attacker);

        Object.DestroyImmediate(attacker);
    }

    [Test]
    public void DamageInfo_SupportsKnockback()
    {
        // Arrange & Act
        var info = new DamageInfo(100f, DamageType.Physical);
        info.knockbackForce = 10f;
        info.knockbackDirection = Vector3.back;

        // Assert
        Assert.AreEqual(10f, info.knockbackForce);
        Assert.AreEqual(Vector3.back, info.knockbackDirection);
    }

    [Test]
    public void DamageInfo_SupportsCritical()
    {
        // Arrange & Act
        var info = new DamageInfo(100f, DamageType.Physical);
        info.isCritical = true;
        info.criticalMultiplier = 2f;

        // Assert
        Assert.IsTrue(info.isCritical);
        Assert.AreEqual(2f, info.criticalMultiplier);
    }

    [Test]
    public void DamageInfo_SupportsStagger()
    {
        // Arrange & Act
        var info = new DamageInfo(100f, DamageType.Physical);
        info.staggerValue = 25f;

        // Assert
        Assert.AreEqual(25f, info.staggerValue);
    }
}

/// <summary>
/// Tests pour HitboxData.
/// </summary>
public class HitboxDataTests
{
    [Test]
    public void HitboxData_StoresSize()
    {
        // Arrange & Act
        var hitbox = ScriptableObject.CreateInstance<HitboxData>();
        hitbox.size = new Vector3(1f, 2f, 1f);

        // Assert
        Assert.AreEqual(new Vector3(1f, 2f, 1f), hitbox.size);

        Object.DestroyImmediate(hitbox);
    }

    [Test]
    public void HitboxData_StoresDamageMultiplier()
    {
        // Arrange & Act
        var hitbox = ScriptableObject.CreateInstance<HitboxData>();
        hitbox.damageMultiplier = 1.5f;

        // Assert
        Assert.AreEqual(1.5f, hitbox.damageMultiplier);

        Object.DestroyImmediate(hitbox);
    }

    [Test]
    public void HitboxData_StoresActiveFrames()
    {
        // Arrange & Act
        var hitbox = ScriptableObject.CreateInstance<HitboxData>();
        hitbox.startFrame = 5;
        hitbox.endFrame = 15;

        // Assert
        Assert.AreEqual(5, hitbox.startFrame);
        Assert.AreEqual(15, hitbox.endFrame);

        Object.DestroyImmediate(hitbox);
    }
}

/// <summary>
/// Tests pour Hitbox component.
/// </summary>
public class HitboxTests
{
    private GameObject _hitboxObject;
    private Hitbox _hitbox;

    [SetUp]
    public void Setup()
    {
        _hitboxObject = new GameObject("TestHitbox");
        _hitbox = _hitboxObject.AddComponent<Hitbox>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_hitboxObject);
    }

    [Test]
    public void Hitbox_IsDisabledByDefault()
    {
        // Assert
        Assert.IsFalse(_hitbox.IsActive);
    }

    [Test]
    public void Hitbox_CanBeActivated()
    {
        // Act
        _hitbox.Activate();

        // Assert
        Assert.IsTrue(_hitbox.IsActive);
    }

    [Test]
    public void Hitbox_CanBeDeactivated()
    {
        // Arrange
        _hitbox.Activate();

        // Act
        _hitbox.Deactivate();

        // Assert
        Assert.IsFalse(_hitbox.IsActive);
    }

    [Test]
    public void Hitbox_TracksHitTargets()
    {
        // Arrange
        var target = new GameObject("Target");
        _hitbox.Activate();

        // Act
        _hitbox.RegisterHit(target);

        // Assert
        Assert.IsTrue(_hitbox.HasHit(target));

        Object.DestroyImmediate(target);
    }

    [Test]
    public void Hitbox_ClearsHitTargetsOnActivate()
    {
        // Arrange
        var target = new GameObject("Target");
        _hitbox.Activate();
        _hitbox.RegisterHit(target);
        _hitbox.Deactivate();

        // Act
        _hitbox.Activate();

        // Assert
        Assert.IsFalse(_hitbox.HasHit(target));

        Object.DestroyImmediate(target);
    }
}

/// <summary>
/// Tests pour Hurtbox component.
/// </summary>
public class HurtboxTests
{
    private GameObject _hurtboxObject;
    private Hurtbox _hurtbox;

    [SetUp]
    public void Setup()
    {
        _hurtboxObject = new GameObject("TestHurtbox");
        _hurtbox = _hurtboxObject.AddComponent<Hurtbox>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_hurtboxObject);
    }

    [Test]
    public void Hurtbox_IsVulnerableByDefault()
    {
        // Assert
        Assert.AreEqual(HurtboxState.Vulnerable, _hurtbox.State);
    }

    [Test]
    public void Hurtbox_CanBeInvincible()
    {
        // Act
        _hurtbox.SetState(HurtboxState.Invincible);

        // Assert
        Assert.AreEqual(HurtboxState.Invincible, _hurtbox.State);
    }

    [Test]
    public void Hurtbox_CanParry()
    {
        // Act
        _hurtbox.SetState(HurtboxState.Parrying);

        // Assert
        Assert.AreEqual(HurtboxState.Parrying, _hurtbox.State);
    }

    [Test]
    public void Hurtbox_CanBlock()
    {
        // Act
        _hurtbox.SetState(HurtboxState.Blocking);

        // Assert
        Assert.AreEqual(HurtboxState.Blocking, _hurtbox.State);
    }

    [Test]
    public void Hurtbox_TakesDamage_WhenVulnerable()
    {
        // Arrange
        float damageTaken = 0f;
        _hurtbox.OnDamageReceived += (info) => damageTaken = info.baseDamage;
        var damageInfo = new DamageInfo(50f, DamageType.Physical);

        // Act
        bool result = _hurtbox.TryReceiveDamage(damageInfo);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(50f, damageTaken);
    }

    [Test]
    public void Hurtbox_IgnoresDamage_WhenInvincible()
    {
        // Arrange
        _hurtbox.SetState(HurtboxState.Invincible);
        float damageTaken = 0f;
        _hurtbox.OnDamageReceived += (info) => damageTaken = info.baseDamage;
        var damageInfo = new DamageInfo(50f, DamageType.Physical);

        // Act
        bool result = _hurtbox.TryReceiveDamage(damageInfo);

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(0f, damageTaken);
    }

    [Test]
    public void Hurtbox_ReducesDamage_WhenBlocking()
    {
        // Arrange
        _hurtbox.SetState(HurtboxState.Blocking);
        _hurtbox.BlockDamageReduction = 0.7f; // 70% reduction
        float damageTaken = 0f;
        _hurtbox.OnDamageReceived += (info) => damageTaken = info.baseDamage;
        var damageInfo = new DamageInfo(100f, DamageType.Physical);

        // Act
        bool result = _hurtbox.TryReceiveDamage(damageInfo);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(30f, damageTaken, 0.01f);
    }

    [Test]
    public void Hurtbox_CountersAttack_WhenParrying()
    {
        // Arrange
        _hurtbox.SetState(HurtboxState.Parrying);
        bool parryTriggered = false;
        _hurtbox.OnParrySuccess += (info) => parryTriggered = true;
        var damageInfo = new DamageInfo(50f, DamageType.Physical);

        // Act
        bool result = _hurtbox.TryReceiveDamage(damageInfo);

        // Assert
        Assert.IsFalse(result); // Damage negated
        Assert.IsTrue(parryTriggered);
    }
}

/// <summary>
/// Tests pour ComboData.
/// </summary>
public class ComboDataTests
{
    [Test]
    public void ComboData_StoresAttackSequence()
    {
        // Arrange
        var combo = ScriptableObject.CreateInstance<ComboData>();
        combo.comboName = "Basic Combo";
        combo.attacks = new List<AttackData>();

        // Assert
        Assert.AreEqual("Basic Combo", combo.comboName);
        Assert.IsNotNull(combo.attacks);

        Object.DestroyImmediate(combo);
    }

    [Test]
    public void ComboData_HasInputWindow()
    {
        // Arrange
        var combo = ScriptableObject.CreateInstance<ComboData>();
        combo.inputWindowDuration = 0.5f;

        // Assert
        Assert.AreEqual(0.5f, combo.inputWindowDuration);

        Object.DestroyImmediate(combo);
    }
}

/// <summary>
/// Tests pour AttackData.
/// </summary>
public class AttackDataTests
{
    [Test]
    public void AttackData_StoresDamage()
    {
        // Arrange
        var attack = ScriptableObject.CreateInstance<AttackData>();
        attack.baseDamage = 25f;
        attack.damageType = DamageType.Physical;

        // Assert
        Assert.AreEqual(25f, attack.baseDamage);
        Assert.AreEqual(DamageType.Physical, attack.damageType);

        Object.DestroyImmediate(attack);
    }

    [Test]
    public void AttackData_StoresKnockback()
    {
        // Arrange
        var attack = ScriptableObject.CreateInstance<AttackData>();
        attack.knockbackForce = 5f;

        // Assert
        Assert.AreEqual(5f, attack.knockbackForce);

        Object.DestroyImmediate(attack);
    }

    [Test]
    public void AttackData_StoresStagger()
    {
        // Arrange
        var attack = ScriptableObject.CreateInstance<AttackData>();
        attack.staggerValue = 10f;

        // Assert
        Assert.AreEqual(10f, attack.staggerValue);

        Object.DestroyImmediate(attack);
    }

    [Test]
    public void AttackData_StoresAnimationInfo()
    {
        // Arrange
        var attack = ScriptableObject.CreateInstance<AttackData>();
        attack.animationTrigger = "Attack1";
        attack.animationDuration = 0.8f;

        // Assert
        Assert.AreEqual("Attack1", attack.animationTrigger);
        Assert.AreEqual(0.8f, attack.animationDuration);

        Object.DestroyImmediate(attack);
    }

    [Test]
    public void AttackData_SupportsChargedAttack()
    {
        // Arrange
        var attack = ScriptableObject.CreateInstance<AttackData>();
        attack.isChargedAttack = true;
        attack.minChargeTime = 0.5f;
        attack.maxChargeTime = 2f;
        attack.chargeMultiplier = 2.5f;

        // Assert
        Assert.IsTrue(attack.isChargedAttack);
        Assert.AreEqual(0.5f, attack.minChargeTime);
        Assert.AreEqual(2f, attack.maxChargeTime);
        Assert.AreEqual(2.5f, attack.chargeMultiplier);

        Object.DestroyImmediate(attack);
    }
}

/// <summary>
/// Tests pour StaggerSystem.
/// </summary>
public class StaggerSystemTests
{
    private GameObject _testObject;
    private StaggerHandler _staggerHandler;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestStagger");
        _staggerHandler = _testObject.AddComponent<StaggerHandler>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void StaggerHandler_HasMaxPoise()
    {
        // Arrange
        _staggerHandler.MaxPoise = 100f;

        // Assert
        Assert.AreEqual(100f, _staggerHandler.MaxPoise);
    }

    [Test]
    public void StaggerHandler_StartsAtMaxPoise()
    {
        // Arrange
        _staggerHandler.MaxPoise = 100f;
        _staggerHandler.ResetPoise();

        // Assert
        Assert.AreEqual(100f, _staggerHandler.CurrentPoise);
    }

    [Test]
    public void StaggerHandler_ReducesPoiseOnHit()
    {
        // Arrange
        _staggerHandler.MaxPoise = 100f;
        _staggerHandler.ResetPoise();

        // Act
        _staggerHandler.ApplyStagger(30f);

        // Assert
        Assert.AreEqual(70f, _staggerHandler.CurrentPoise);
    }

    [Test]
    public void StaggerHandler_TriggersStagger_WhenPoiseBreaks()
    {
        // Arrange
        _staggerHandler.MaxPoise = 100f;
        _staggerHandler.ResetPoise();
        bool staggerTriggered = false;
        _staggerHandler.OnStaggered += () => staggerTriggered = true;

        // Act
        _staggerHandler.ApplyStagger(100f);

        // Assert
        Assert.IsTrue(staggerTriggered);
        Assert.IsTrue(_staggerHandler.IsStaggered);
    }

    [Test]
    public void StaggerHandler_RegeneratesPoise()
    {
        // Arrange
        _staggerHandler.MaxPoise = 100f;
        _staggerHandler.PoiseRegenRate = 10f;
        _staggerHandler.ResetPoise();
        _staggerHandler.ApplyStagger(50f);

        // Act
        _staggerHandler.RegeneratePoise(1f); // Simulate 1 second

        // Assert
        Assert.AreEqual(60f, _staggerHandler.CurrentPoise);
    }
}

/// <summary>
/// Tests pour DamageCalculator.
/// </summary>
public class DamageCalculatorTests
{
    [Test]
    public void CalculateDamage_ReturnsBaseDamage_WithNoModifiers()
    {
        // Arrange
        var info = new DamageInfo(100f, DamageType.Physical);
        float defense = 0f;

        // Act
        float damage = DamageCalculator.CalculateFinalDamage(info, defense);

        // Assert
        Assert.AreEqual(100f, damage);
    }

    [Test]
    public void CalculateDamage_ReducesByDefense()
    {
        // Arrange
        var info = new DamageInfo(100f, DamageType.Physical);
        float defense = 50f;

        // Act
        float damage = DamageCalculator.CalculateFinalDamage(info, defense);

        // Assert
        Assert.Less(damage, 100f);
    }

    [Test]
    public void CalculateDamage_AppliesCriticalMultiplier()
    {
        // Arrange
        var info = new DamageInfo(100f, DamageType.Physical);
        info.isCritical = true;
        info.criticalMultiplier = 2f;
        float defense = 0f;

        // Act
        float damage = DamageCalculator.CalculateFinalDamage(info, defense);

        // Assert
        Assert.AreEqual(200f, damage);
    }

    [Test]
    public void CalculateDamage_NeverGoesBelowMinimum()
    {
        // Arrange
        var info = new DamageInfo(10f, DamageType.Physical);
        float defense = 9999f;

        // Act
        float damage = DamageCalculator.CalculateFinalDamage(info, defense);

        // Assert
        Assert.GreaterOrEqual(damage, 1f);
    }

    [Test]
    public void CalculateCriticalChance_DefaultIs5Percent()
    {
        // Act
        float critChance = DamageCalculator.GetBaseCriticalChance();

        // Assert
        Assert.AreEqual(0.05f, critChance);
    }
}

/// <summary>
/// Tests pour KnockbackSystem.
/// </summary>
public class KnockbackTests
{
    private GameObject _testObject;
    private KnockbackReceiver _receiver;
    private Rigidbody _rb;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestKnockback");
        _rb = _testObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _receiver = _testObject.AddComponent<KnockbackReceiver>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void KnockbackReceiver_AppliesForce()
    {
        // Arrange
        Vector3 direction = Vector3.back;
        float force = 10f;

        // Act
        _receiver.ApplyKnockback(direction, force);

        // Assert - Velocity should be non-zero in the knockback direction
        // Note: In EditMode, physics doesn't run, so we check the pending knockback
        Assert.IsTrue(_receiver.HasPendingKnockback);
    }

    [Test]
    public void KnockbackReceiver_CanBeImmune()
    {
        // Arrange
        _receiver.IsImmune = true;

        // Act
        _receiver.ApplyKnockback(Vector3.back, 10f);

        // Assert
        Assert.IsFalse(_receiver.HasPendingKnockback);
    }

    [Test]
    public void KnockbackReceiver_SupportsKnockbackResistance()
    {
        // Arrange
        _receiver.KnockbackResistance = 0.5f; // 50% resistance

        // Act
        _receiver.ApplyKnockback(Vector3.back, 10f);

        // Assert
        Assert.AreEqual(5f, _receiver.PendingKnockbackForce);
    }
}

/// <summary>
/// Tests pour CombatState.
/// </summary>
public class CombatStateTests
{
    [Test]
    public void CombatState_HasAllRequiredStates()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Idle));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Attacking));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Blocking));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Parrying));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Dodging));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Staggered));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CombatState), CombatState.Charging));
    }
}

/// <summary>
/// Tests pour CombatController.
/// </summary>
public class CombatControllerTests
{
    private GameObject _testObject;
    private CombatController _combat;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestCombat");
        _combat = _testObject.AddComponent<CombatController>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void CombatController_StartsInIdleState()
    {
        // Assert
        Assert.AreEqual(CombatState.Idle, _combat.CurrentState);
    }

    [Test]
    public void CombatController_TracksComboCount()
    {
        // Assert
        Assert.AreEqual(0, _combat.CurrentComboCount);
    }

    [Test]
    public void CombatController_CanStartAttack()
    {
        // Act
        bool canAttack = _combat.CanAttack();

        // Assert - Should be able to attack from Idle
        Assert.IsTrue(canAttack);
    }

    [Test]
    public void CombatController_CanBlock()
    {
        // Act
        bool canBlock = _combat.CanBlock();

        // Assert
        Assert.IsTrue(canBlock);
    }

    [Test]
    public void CombatController_CanDodge()
    {
        // Act
        bool canDodge = _combat.CanDodge();

        // Assert
        Assert.IsTrue(canDodge);
    }
}
