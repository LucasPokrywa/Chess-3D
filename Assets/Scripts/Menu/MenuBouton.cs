using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Effets visuels d'un bouton du menu : grossit et s'illumine quand il a le focus
// (souris, clavier ou manette). "Actif" garde la bordure allumée (option choisie).
[RequireComponent(typeof(Selectable))]
public class MenuBouton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    public Image fond;
    public Graphic bordure;
    public Text texte;

    public Color fondNormal = new Color(0f, 0f, 0f, 0.55f);
    public Color fondFocus = new Color(0.2f, 0.15f, 0.1f, 0.85f);
    public Color bordureNormal = new Color(1f, 1f, 1f, 0.15f);
    public Color bordureFocus = Color.white;
    public Color texteNormal = new Color(0.85f, 0.85f, 0.85f);
    public Color texteFocus = Color.white;

    public float echelleFocus = 1.06f;
    public bool Actif;

    private Selectable selectable;
    private CanvasGroup groupe;
    private bool focus;
    private float tFocus;
    private float tActif;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        groupe = GetComponent<CanvasGroup>();
        if (groupe == null) groupe = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // La souris donne le focus : souris et clavier partagent la même surbrillance.
        if (selectable.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => focus = true;
    public void OnDeselect(BaseEventData eventData) => focus = false;

    private void Update()
    {
        bool interactif = selectable.IsInteractable();
        float vitesse = Time.unscaledDeltaTime * 8f;
        tFocus = Mathf.MoveTowards(tFocus, focus && interactif ? 1f : 0f, vitesse);
        tActif = Mathf.MoveTowards(tActif, Actif ? 1f : 0f, vitesse);

        float f = Mathf.SmoothStep(0f, 1f, tFocus);
        float a = Mathf.SmoothStep(0f, 1f, tActif);
        float allume = Mathf.Max(f, a);

        if (fond != null) fond.color = Color.Lerp(fondNormal, fondFocus, allume);
        if (bordure != null)
        {
            Color c = Color.Lerp(bordureNormal, bordureFocus, allume);
            // Légère pulsation de la bordure de l'option choisie.
            if (a > 0f) c.a *= Mathf.Lerp(1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f), a);
            bordure.color = c;
        }
        if (texte != null) texte.color = Color.Lerp(texteNormal, texteFocus, allume);

        transform.localScale = Vector3.one * Mathf.Lerp(1f, echelleFocus, f);
        // Grisé seulement si le bouton lui-même est désactivé (pas pendant un fondu d'écran).
        groupe.alpha = selectable.interactable ? 1f : 0.45f;
    }
}
