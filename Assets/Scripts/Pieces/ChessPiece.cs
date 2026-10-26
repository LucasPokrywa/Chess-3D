using System;
using System.Collections;
using UnityEngine;

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

// Une pièce posée sur l'échiquier. Sa position logique (X, Z)
// correspond aux coordonnées des BoardTile.
public class ChessPiece : MonoBehaviour
{
    public PieceType Type { get; private set; }
    public bool IsWhite { get; private set; }
    public int X { get; private set; }
    public int Z { get; private set; }
    public bool HasMoved { get; set; }
    public bool IsMoving { get; private set; }

    private Vector3 restPosition;
    private float liftAmount;
    private Coroutine moveRoutine;

    public void Initialize(PieceType type, bool isWhite, int x, int z)
    {
        Type = type;
        IsWhite = isWhite;
        X = x;
        Z = z;
        restPosition = transform.localPosition;
    }

    public void SetSquare(int x, int z)
    {
        X = x;
        Z = z;
    }

    // Soulève légèrement la pièce quand elle est sélectionnée.
    public void SetLifted(bool lifted, float height)
    {
        if (IsMoving)
            return;

        liftAmount = lifted ? height : 0f;
        transform.localPosition = restPosition + Vector3.up * liftAmount;
    }

    // Déplacement en arc vers une position locale (relative au plateau).
    public void MoveTo(
        Vector3 localTarget,
        float duration,
        float arcHeight,
        Action onComplete = null)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(
            MoveRoutine(localTarget, duration, arcHeight, onComplete)
        );
    }

    private IEnumerator MoveRoutine(
        Vector3 target,
        float duration,
        float arcHeight,
        Action onComplete)
    {
        IsMoving = true;

        Vector3 start = transform.localPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / duration);
            float eased = t * t * (3f - 2f * t);

            Vector3 position = Vector3.Lerp(start, target, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.localPosition = position;
            yield return null;
        }

        transform.localPosition = target;
        restPosition = target;
        liftAmount = 0f;
        IsMoving = false;
        moveRoutine = null;

        onComplete?.Invoke();
    }

    // Petite animation de disparition lors d'une capture.
    public void Capture(float duration)
    {
        StartCoroutine(CaptureRoutine(duration));
    }

    private IEnumerator CaptureRoutine(float duration)
    {
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.localPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            transform.localScale = startScale * (1f - t);
            transform.localPosition = startPosition + Vector3.up * (t * 0.4f);
            yield return null;
        }

        Destroy(gameObject);
    }
}
