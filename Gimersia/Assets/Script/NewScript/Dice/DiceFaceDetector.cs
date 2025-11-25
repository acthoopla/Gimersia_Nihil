using UnityEngine;

public class DiceFaceDetector : MonoBehaviour
{
    [Tooltip("Drag child Face_1 s/d Face_6 ke sini")]
    public Transform[] faces;

    public int CalculateResult()
    {
        if (faces == null || faces.Length == 0)
        {
            Debug.LogError("[DiceCalculator] Faces belum di-assign!");
            return 1;
        }

        int bestFace = 1;
        float highestY = -Mathf.Infinity;

        // Logika: Cari face dengan posisi Y tertinggi di World Space
        foreach (Transform t in faces)
        {
            if (t.position.y > highestY)
            {
                highestY = t.position.y;
                // Parsing nama object "Face_X"
                string[] split = t.name.Split('_');
                if (split.Length > 1 && int.TryParse(split[1], out int val))
                {
                    bestFace = val;
                }
            }
        }
        return bestFace;
    }
}