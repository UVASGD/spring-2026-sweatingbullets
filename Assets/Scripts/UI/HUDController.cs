using System.Collections;
using Player;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDController : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private PlayerPickupController pickupController;
    [SerializeField, Min(0f)] private float initialPromptDuration = 5f;
    [SerializeField, Min(0f)] private float initialPromptFadeDuration = 1f;

    private UIDocument _document;
    private Label _initialGuideLabel;
    private Label _pickupPromptLabel;
    private Label _hammerPromptLabel;
    private Coroutine _initialFadeRoutine;
    private bool _subscribed;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
        if (weaponController == null)
            weaponController = FindAnyObjectByType<WeaponController>();
        if (pickupController == null)
            pickupController = FindAnyObjectByType<PlayerPickupController>();
    }

    private void OnEnable()
    {
        CacheLabels();
        Subscribe();
        RefreshPickupPrompt(pickupController != null ? pickupController.CurrentNearest : null);
        RefreshHammerPrompt();
    }

    private void Start()
    {
        if (_initialGuideLabel == null)
            return;

        bool alreadyArmed = weaponController != null
                            && weaponController.HasWeapon
                            && weaponController.AmmoCount > 0;

        if (alreadyArmed)
        {
            _initialGuideLabel.style.display = DisplayStyle.None;
            return;
        }

        _initialGuideLabel.style.display = DisplayStyle.Flex;
        _initialGuideLabel.style.opacity = 1f;
        if (_initialFadeRoutine != null)
            StopCoroutine(_initialFadeRoutine);
        _initialFadeRoutine = StartCoroutine(FadeOutInitialGuide());
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (_initialFadeRoutine != null)
        {
            StopCoroutine(_initialFadeRoutine);
            _initialFadeRoutine = null;
        }
    }

    private void CacheLabels()
    {
        if (_document == null)
            _document = GetComponent<UIDocument>();
        VisualElement root = _document != null ? _document.rootVisualElement : null;
        if (root == null)
            return;

        _initialGuideLabel = root.Q<Label>("InitialGuideLabel");
        _pickupPromptLabel = root.Q<Label>("PickupPromptLabel");
        _hammerPromptLabel = root.Q<Label>("HammerPromptLabel");
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        if (weaponController != null)
        {
            weaponController.OnGunPickedUp += HandleGunPickedUp;
            weaponController.OnAmmoChanged += HandleAmmoChanged;
            weaponController.OnHammerCockedChanged += HandleHammerCockedChanged;
        }

        if (pickupController != null)
            pickupController.OnNearestPickupChanged += HandleNearestPickupChanged;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        if (weaponController != null)
        {
            weaponController.OnGunPickedUp -= HandleGunPickedUp;
            weaponController.OnAmmoChanged -= HandleAmmoChanged;
            weaponController.OnHammerCockedChanged -= HandleHammerCockedChanged;
        }

        if (pickupController != null)
            pickupController.OnNearestPickupChanged -= HandleNearestPickupChanged;

        _subscribed = false;
    }

    private void HandleGunPickedUp()
    {
        HideInitialGuideIfArmed();
        RefreshHammerPrompt();
    }

    private void HandleAmmoChanged(int _)
    {
        HideInitialGuideIfArmed();
        RefreshHammerPrompt();
    }

    private void HandleHammerCockedChanged(bool _)
    {
        RefreshHammerPrompt();
    }

    private void HandleNearestPickupChanged(PickupItem pickup)
    {
        RefreshPickupPrompt(pickup);
    }

    private void RefreshPickupPrompt(PickupItem pickup)
    {
        if (_pickupPromptLabel == null)
            return;

        if (pickup == null)
        {
            _pickupPromptLabel.style.display = DisplayStyle.None;
            return;
        }

        string itemName = pickup.Type == PickupItem.PickupType.Gun ? "Gun" : "Bullets";
        _pickupPromptLabel.text = $"Press [E] to pick up {itemName}";
        _pickupPromptLabel.style.display = DisplayStyle.Flex;
    }

    private void RefreshHammerPrompt()
    {
        if (_hammerPromptLabel == null)
            return;

        bool show = weaponController != null
                    && weaponController.HasWeapon
                    && weaponController.AmmoCount > 0
                    && !weaponController.IsHammerCocked
                    && !weaponController.IsEquippingWeapon;

        _hammerPromptLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void HideInitialGuideIfArmed()
    {
        if (_initialGuideLabel == null || weaponController == null)
            return;

        if (!weaponController.HasWeapon || weaponController.AmmoCount <= 0)
            return;

        if (_initialFadeRoutine != null)
        {
            StopCoroutine(_initialFadeRoutine);
            _initialFadeRoutine = null;
        }
        _initialGuideLabel.style.display = DisplayStyle.None;
    }

    private IEnumerator FadeOutInitialGuide()
    {
        float wait = initialPromptDuration;
        while (wait > 0f)
        {
            wait -= Time.unscaledDeltaTime;
            yield return null;
        }

        float fade = Mathf.Max(0.0001f, initialPromptFadeDuration);
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.unscaledDeltaTime;
            _initialGuideLabel.style.opacity = Mathf.Clamp01(1f - elapsed / fade);
            yield return null;
        }

        _initialGuideLabel.style.opacity = 0f;
        _initialGuideLabel.style.display = DisplayStyle.None;
        _initialFadeRoutine = null;
    }
}
