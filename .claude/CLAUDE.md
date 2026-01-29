# EpicLegends - Claude Project Memory

## Project Overview
- **Name**: EpicLegends
- **Type**: Action-RPG 3D
- **Target Platform**: Windows PC (primary), WebGL (secondary)
- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Developer**: Sylvain (debutant Unity/C#)

## Quick Reference

### Critical Rules
- NEVER use GetComponent() in Update/FixedUpdate - cache in Awake/Start
- NEVER modify transform directly on physics objects - use Rigidbody
- ALWAYS use SerializeField instead of public for Inspector fields
- ALWAYS write tests for gameplay logic
- Keep scripts under 200 lines - split if larger

### Project Structure
```
Assets/
├── Scripts/
│   ├── Player/       # Player controller, stats, abilities
│   ├── Enemies/      # Enemy AI, behaviors, spawning
│   ├── Combat/       # Damage system, weapons, skills
│   ├── UI/           # HUD, menus, dialogs
│   ├── Inventory/    # Items, equipment, loot
│   ├── Quests/       # Quest system, objectives
│   ├── Core/         # Managers, singletons, events
│   └── Utils/        # Helper functions, extensions
├── Tests/
│   ├── EditMode/     # Unit tests (pure logic)
│   └── PlayMode/     # Integration tests (GameObjects)
├── Prefabs/          # Reusable GameObjects
├── Materials/        # Visual materials
├── Animations/       # Animation clips & controllers
├── Audio/
│   ├── Music/
│   └── SFX/
├── Scenes/           # Game scenes
└── ScriptableObjects/
    ├── Items/        # Item data
    ├── Enemies/      # Enemy configs
    └── Skills/       # Skill definitions
```

## Git Workflow

### Branches
- `main` : Production-ready, playable builds
- `core/systems` : Core game systems
- `feature/*` : Individual features

### Commit Convention
```
feat(scope): description   # New feature
fix(scope): description    # Bug fix
refactor(scope): desc      # Restructuring
test(scope): desc          # Tests
docs(scope): desc          # Documentation
```

## Unity Setup

### MCP Server
- Server: unity-mcp (CoplayDev)
- Status: Configured in Claude Code
- Install package in Unity: Window > Package Manager > + > Add from git URL:
  `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`

### Required Packages
- TextMeshPro (UI text)
- Cinemachine (Camera)
- Input System (New input)
- Test Framework (Testing)

## Development Rules

### Code Quality Gates (for Ralph Loop)
All code must pass before completion:
1. No compilation errors
2. No console warnings
3. All unit tests pass
4. Follows naming conventions
5. Public methods documented

### C# Naming Conventions
```csharp
// Classes: PascalCase
public class PlayerController : MonoBehaviour { }

// Private fields: _camelCase
private float _moveSpeed;
private Rigidbody _rb;

// Public properties: PascalCase
public float Health { get; private set; }

// Methods: PascalCase
public void TakeDamage(float amount) { }

// Constants: UPPER_SNAKE_CASE
private const float MAX_HEALTH = 100f;

// SerializeField pattern
[SerializeField] private float moveSpeed = 5f;
```

### Component Caching Pattern
```csharp
private Rigidbody _rb;
private Animator _animator;

private void Awake()
{
    _rb = GetComponent<Rigidbody>();
    _animator = GetComponent<Animator>();
}
```

### Singleton Pattern (for managers)
```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

## Ralph Loop Templates

### For Scripts
```
/ralph-loop "
Create [SCRIPT_NAME] with:
- [Requirement 1]
- [Requirement 2]
- Tests in EditMode

SUCCESS CRITERIA:
- Compiles with 0 errors
- 0 console warnings
- Tests pass
- Follows conventions

<promise>SCRIPT_COMPLETE</promise>
" --max-iterations 25
```

### For Features
```
/ralph-loop "
Implement [FEATURE] for EpicLegends RPG:
- [Feature spec 1]
- [Feature spec 2]
- Integration with existing systems

SUCCESS CRITERIA:
- Feature works as specified
- Tests cover main paths
- No regression
- Documentation updated

<promise>FEATURE_COMPLETE</promise>
" --max-iterations 30
```

## RPG-Specific Systems

### Player Stats
- Health, Mana, Stamina
- Strength, Agility, Intelligence
- Level, Experience
- Equipment slots

### Combat System
- Melee attacks
- Ranged attacks
- Skills/abilities
- Damage types (physical, magical, etc.)

### Inventory System
- Item types (weapon, armor, consumable, quest)
- Stacking
- Equipment comparison
- Loot drops

### Enemy AI
- Patrol behavior
- Chase when in range
- Attack patterns
- Drop loot on death

## Unity Learnings

### Physics
[Auto-populated via /reflect]

### UI
[Auto-populated via /reflect]

### AI/Behavior
[Auto-populated via /reflect]

### Performance
[Auto-populated via /reflect]

### Common Pitfalls
[Auto-populated via /reflect]

---

**Last Updated**: 2026-01-29
**Version**: 1.0
