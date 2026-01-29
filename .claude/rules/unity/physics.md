---
paths:
  - "Assets/Scripts/**/*Movement*.cs"
  - "Assets/Scripts/**/*Physics*.cs"
  - "Assets/Scripts/**/*Controller*.cs"
  - "Assets/Scripts/Player/**/*.cs"
  - "Assets/Scripts/Enemies/**/*.cs"
---

# Unity Physics Rules

## Never
- NEVER modify transform.position directly for physics objects
- NEVER use Rigidbody with kinematic = true AND move via transform
- NEVER put physics code in Update() (use FixedUpdate())
- NEVER use Find/GetComponent in FixedUpdate

## Always
- ALWAYS use Rigidbody.MovePosition() or AddForce()
- ALWAYS cache Rigidbody references in Awake/Start
- ALWAYS use FixedUpdate for physics calculations
- ALWAYS use LayerMask for raycasts

## Ground Check Pattern
```csharp
[SerializeField] private float groundCheckDistance = 0.1f;
[SerializeField] private LayerMask groundLayer;

private bool IsGrounded()
{
    return Physics.Raycast(
        transform.position,
        Vector3.down,
        groundCheckDistance,
        groundLayer
    );
}
```

## Movement Pattern
```csharp
private void FixedUpdate()
{
    Vector3 movement = new Vector3(_moveInput.x, 0, _moveInput.y);
    _rb.MovePosition(_rb.position + movement * _speed * Time.fixedDeltaTime);
}
```

## Jump Pattern
```csharp
public void Jump()
{
    if (IsGrounded())
    {
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
    }
}
```
