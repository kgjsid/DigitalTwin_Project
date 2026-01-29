using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class BlueNoiseGenerator : MonoBehaviour
{
    private void Start()
    {
        Generate();
    }

    public void Generate()
    {
        int size = 256;
        int iterations = 3000;
        int initialPointPercentage = 50;
        float gaussianRadius = 8f;
        float gaussianSigma = 1.8f;

        Texture2D texture = GenerateBlueNoiseTexture(size, iterations, initialPointPercentage, gaussianRadius, gaussianSigma);

        if (texture != null)
        {
            byte[] pngData = texture.EncodeToPNG();
            string path = "Assets/BlueNoise.png";
            File.WriteAllBytes(path, pngData);
            AssetDatabase.Refresh();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public List<int> index;

    public Texture2D GenerateBlueNoiseTexture(int size, int iterations, int initialPointPercentage, float gaussianRadius, float gaussianSigma)
    {
        int numPixels = size * size;

        bool[] binaryPattern = new bool[numPixels];
        System.Random random = new System.Random();
        index = Enumerable.Range(0, numPixels).ToList();

        for (int i = 0; i < numPixels; i++)
        {
            int randomIndex = random.Next(i, numPixels);
            int temp = index[i];
            index[i] = index[randomIndex];
            index[randomIndex] = temp;
        }

        // 일부 픽셀 활성화(50%)
        int numOnPixels = numPixels * initialPointPercentage / 100;
        for (int i = 0; i < numOnPixels; i++)
        {
            binaryPattern[index[i]] = true;
        }

        float[] density = null;

        for (int i = 0; i < iterations; i++)
        {
            // 가우시안 필터로 블러처리된 결과 배열
            density = ApplyGaussian(binaryPattern, size, (int)gaussianRadius, gaussianSigma);

            // 밀도가 높은 지역과 낮은 지역을 찾아 그 위치 교환
            int densestIdx = -1;
            int voidestIdx = -1;
            float maxDensity = -1.0f;
            float minDensity = 999.0f;

            // numPixels : texture size(64 x 64)
            for (int j = 0; j < numPixels; j++)
            {
                // binaryPattern : 기존 이진 픽셀 배열(true / false)
                // density : 이진 픽셀 배열에 가우시안 필터를 적용한 결과
                // maxDensity : 최대 밀도값 -> 픽셀 중 가장 진한 값(1f에 가까운 값)
                if (binaryPattern[j] && density[j] > maxDensity)
                {
                    maxDensity = density[j];
                    densestIdx = j;
                }

                // minDensity : 최소 밀도값 -> 픽셀이 거의 비어있는 곳
                if (!binaryPattern[j] && density[j] < minDensity)
                {
                    minDensity = density[j];
                    voidestIdx = j;
                }
            }

            // 둘 위치를 변경
            if (densestIdx != -1 && voidestIdx != -1)
            {
                bool temp = binaryPattern[densestIdx];
                binaryPattern[densestIdx] = binaryPattern[voidestIdx];
                binaryPattern[voidestIdx] = temp;
            }
            else
            {
                break;
            }
        }

        // 픽셀 순위 결정
        int[] ranks = new int[numPixels];
        bool[] currentPattern = (bool[])binaryPattern.Clone();
        int rankCounter = 0;

        for (int i = 0; i < numOnPixels; i++)
        {
            // 가우시안 블러 적용
            density = ApplyGaussian(currentPattern, size, (int)gaussianRadius, gaussianSigma);

            int densestIdx = -1;
            float maxDensity = -1.0f;

            for (int j = 0; j < numPixels; j++)
            {
                // 픽셀 max값(값이 1f에 근접한 픽셀) 인덱스 저장
                if (currentPattern[j] && density[j] > maxDensity)
                {
                    maxDensity = density[j];
                    densestIdx = j;
                }
            }

            // ex) 제일 처음 densestIdx : 10이라고 가정.
            // 64 * 64 - 1 => 4095 - 0
            // ranks[10] = 4095 대입
            // pattern = false(0)? -> 밀집된 픽셀 제거? -> 다음 반복시 판단하지 않음.(ranks에 저장되어 있음)
            if (densestIdx != -1)
            {
                ranks[densestIdx] = numPixels - 1 - rankCounter;
                currentPattern[densestIdx] = false;
                rankCounter++;
            }
        }

        // void 기준 픽셀 순위 결정
        currentPattern = (bool[])binaryPattern.Clone();
        int numRemaining = numPixels - numOnPixels;

        for (int i = 0; i < numRemaining; i++)
        {
            density = ApplyGaussian(currentPattern, size, (int)gaussianRadius, gaussianSigma);
            int voidestIdx = -1;
            float minDensity = 999.0f;

            for (int j = 0; j < numPixels; j++)
            {
                if (!currentPattern[j] && density[j] < minDensity)
                {
                    minDensity = density[j];
                    voidestIdx = j;
                }
            }

            if (voidestIdx != -1)
            {
                ranks[voidestIdx] = rankCounter;
                currentPattern[voidestIdx] = true;
                rankCounter++;
            }
        }

        // Rank별로 1 ~ 0값 부여.(Rank가 클 수록 1에 가까운 값)
        colors = new Color[numPixels];
        for (int i = 0; i < numPixels; i++)
        {
            float rankValue = (float)ranks[i] / ((numPixels - 1));
            colors[i] = new Color(rankValue, rankValue, rankValue, 1.0f);
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels(colors);
        texture.Apply();

        return texture;
    }

    public Color[] colors;

    // 가우시안 분포를 활용한 블러(필터링)처리
    private float[] ApplyGaussian(bool[] image, int width, int radius, float sigma)
    {
        // 블러 결과
        float[] blurred = new float[image.Length];
        // 가우시안 커널(필터)
        float[] kernel = CreateGaussianKernel(radius, sigma);

        // 수평 블러
        float[] horizontalBlurred = new float[image.Length];

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = 0.0f;

                for (int i = -radius; i <= radius; i++)
                {
                    // 가로 블러 계산(radius : 4 -> 가로로 9개의 픽셀을 가져와서 가중치 처리) 
                    int neighborX = (x + i + width) % width;
                    value += (image[y * width + neighborX] ? 1.0f : 0.0f) * kernel[i + radius];
                }

                horizontalBlurred[y * width + x] = value;
            }
        }

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = 0.0f;

                for (int i = -radius; i <= radius; i++)
                {
                    // 세로 블러 계산
                    int neighborY = (y + i + width) % width;
                    value += horizontalBlurred[neighborY * width + x] * kernel[i + radius];
                }

                blurred[y * width + x] = value;
            }
        }

        return blurred;
    }

    // 가우시안 필터 생성
    private float[] CreateGaussianKernel(int radius, float sigma)
    {
        int size = 2 * radius + 1;
        float[] kernel = new float[size];
        float sum = 0.0f;

        for (int i = 0; i < size; i++)
        {
            float x = i - radius;
            // 가우시안 분포의 지수 -> 중심(x : 0)에서 멀리 있을 수록 낮은 가중치
            // sigma(편차)가 작을수록 중앙에 몰리는 구조
            kernel[i] = Mathf.Exp(-(x * x) / (2 * sigma * sigma));
            sum += kernel[i];
        }

        // 모든 커널 요소의 합을 1로 정규화
        for (int i = 0; i < size; i++)
        {
            kernel[i] /= sum;
        }
        // radius : 4 -> 9개의 필터
        // 중간은 0.22의 가중치 양끝으로 갈수록 가중치가 낮아지는 형태.
        // 만약 radius : 0 -> 필터 의미 없음(원본 픽셀 사용)

        return kernel;
    }
}