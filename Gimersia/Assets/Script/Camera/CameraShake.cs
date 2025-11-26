using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    [Header("General Shake Settings")]
    [Range(0f, 2f)]
    public float shakeIntensityMultiplier = 1f;
    [Range(0.1f, 2f)]
    public float shakeDuration = 0.8f;
    [Range(0.1f, 1f)]
    public float shakeMagnitude = 0.8f;
    [Range(3f, 20f)]
    public float shakeRoughness = 15f;

    [Range(1f, 5f)]
    public float traumaRecoverySpeed = 2f;
    [Range(0f, 2f)]
    public float rotationMultiplier = 1f;
    public bool enableRotationShake = true;

    private Vector3 originalPos;
    private Quaternion originalRot;
    private bool isShaking = false;

    private void Start()
    {
        originalPos = transform.localPosition;
        originalRot = transform.localRotation;
    }

    public void ShakeCamera(float duration, float magnitude, float roughness)
    {
        if (!isShaking)
        {
            StartCoroutine(Shake(duration, magnitude * shakeIntensityMultiplier, roughness));
        }
    }

    public void ShakeCameraTrauma(float trauma)
    {
        StartCoroutine(ShakeWithTrauma(trauma * shakeIntensityMultiplier));
    }

    private IEnumerator Shake(float duration, float magnitude, float roughness)
    {
        isShaking = true;
        float elapsed = 0f;
        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percentComplete = elapsed / duration;

            float damper = 1f - Mathf.Clamp01(percentComplete);

            float x = (Mathf.PerlinNoise(offsetX + elapsed * roughness, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, offsetY + elapsed * roughness) - 0.5f) * 2f;

            x *= magnitude * damper;
            y *= magnitude * damper;

            transform.localPosition = originalPos + new Vector3(x, y, 0f);

            if (enableRotationShake)
            {
                float rotZ = (Mathf.PerlinNoise(elapsed * roughness, elapsed * roughness) - 0.5f) * magnitude * 10f * damper * rotationMultiplier;
                transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, rotZ);
            }

            yield return null;
        }

        transform.localPosition = originalPos;
        transform.localRotation = originalRot;
        isShaking = false;
    }

    private IEnumerator ShakeWithTrauma(float trauma)
    {
        isShaking = true;
        float shake = trauma;

        while (shake > 0f)
        {
            shake -= Time.deltaTime * traumaRecoverySpeed;
            shake = Mathf.Clamp01(shake);

            float shakeAmount = shake * shake;

            float offsetX = Random.Range(-1f, 1f) * shakeAmount * 0.3f;
            float offsetY = Random.Range(-1f, 1f) * shakeAmount * 0.3f;

            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            if (enableRotationShake)
            {
                float rotZ = Random.Range(-1f, 1f) * shakeAmount * 5f * rotationMultiplier;
                transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, rotZ);
            }

            yield return null;
        }

        transform.localPosition = originalPos;
        transform.localRotation = originalRot;
        isShaking = false;
    }

    public void HeavyImpactShake()
    {
        StartCoroutine(HeavyImpact());
    }

    private IEnumerator HeavyImpact()
    {
        isShaking = true;

        float impactMag = shakeMagnitude * shakeIntensityMultiplier;
        transform.localPosition = originalPos + new Vector3(
            Random.Range(-impactMag, impactMag),
            Random.Range(-impactMag, impactMag),
            0f
        );

        yield return new WaitForSeconds(0.05f);

        yield return StartCoroutine(Shake(shakeDuration, shakeMagnitude * shakeIntensityMultiplier, shakeRoughness));

        isShaking = false;
    }

    [ContextMenu("GolemPunchShake")]
    public void GolemPunchShake()
    {
        ShakeCamera(shakeDuration, shakeMagnitude, shakeRoughness);
    }

    [ContextMenu("GolemStompShake")]
    public void GolemStompShake()
    {
        HeavyImpactShake();
    }

    [ContextMenu("GolemRoarShake")]
    public void GolemRoarShake()
    {
        ShakeCamera(shakeDuration, shakeMagnitude, shakeRoughness);
    }

    [ContextMenu("GolemGroundSlamShake")]
    public void GolemGroundSlamShake()
    {
        ShakeCameraTrauma(shakeIntensityMultiplier * 1.5f);
    }
}
