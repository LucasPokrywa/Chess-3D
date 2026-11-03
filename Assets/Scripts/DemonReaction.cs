using System.Collections;
using UnityEngine;

public class DemonReaction : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string boolName = "BlackPieceCaptured";

    private int boolHash;

    private void Awake()
    {
        boolHash = Animator.StringToHash(boolName);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  => ChessPiece.OnAnyPieceCaptured += OnCaptured;
    private void OnDisable() => ChessPiece.OnAnyPieceCaptured -= OnCaptured;

    private void OnCaptured(ChessPiece piece)
    {
        // true uniquement si la pièce mangée est NOIRE.
        bool blackCaptured = !piece.IsWhite;

        if (blackCaptured)
            StartCoroutine(PulseBool());
    }

    private IEnumerator PulseBool()
    {
        animator.SetBool(boolHash, true);
        yield return null;          // on laisse l'Animator lire la valeur
        yield return null;          // (2 frames par sécurité)
        animator.SetBool(boolHash, false);
    }
}