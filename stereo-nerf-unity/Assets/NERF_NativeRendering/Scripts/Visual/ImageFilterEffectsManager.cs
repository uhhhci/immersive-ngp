using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Microsoft.MixedReality.Toolkit.UI;
public class ImageFilterEffectsManager : MonoBehaviour
{
	//This is a fun feature which allows to apply different common post processing effects to the stereoscopic images, such as edge filter, inverse filter, etc...

	public Material LeftEye;
	public Material RightEye;
	public GameObject settings;
	public PinchSlider HueSlider;
	public PinchSlider BrightnessSlider;
	public PinchSlider ContrastSlider;
	public PinchSlider SaturationSlider;

	// Shader related stuffs
	Vector4 m_PixelDistMulAdd = new Vector4(1, 1, 1, 0);
	Matrix4x4 m_Color4x4 = new Matrix4x4();
	Matrix4x4 m_HueMatrix = new Matrix4x4(), m_SVCMatrix = new Matrix4x4();
	Vector3 m_Luminance = new Vector3(0.2126f, 0.7152f, 0.0722f);
	Vector4 m_HueSatValCont = Vector4.one;
	KernelMatrix m_KernelMatrix = KernelMatrix.Original;
	float[] m_Kernel3x3 = new float[9];
	float m_KMul = 1;

	// private update attributes
	float hue_val = 0;
	float saturation_val = 0;
	float brightness_val = 0;
	float contrast_val = 0;

	private InputDevice XRRightController;
	bool lastPressedState = false;

	public enum KernelMatrix
	{
		Original,
		Sharp,
		Edge,
		Emboss,
		BoxBlur,
		GaussianBlur,
	}

	// Start is called before the first frame update
	void Start()
	{
		SetOrigin();
		GetRightController();

	}

	// Update is called once per frame
	void Update()
	{
        if (XRRightController != null)
        {
			bool isActive = settings.active;
			bool primaryButtonState;
			XRRightController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonState);
 
			if(primaryButtonState && lastPressedState == false)
            {
				lastPressedState = true;
				settings.SetActive(!isActive);
            }

			if(!primaryButtonState && lastPressedState == true)
            {
				lastPressedState = false;
			}

		}
        else
        {
			GetRightController();
        }
	}


	void GetRightController()
	{
		var rightHandedControllers = new List<InputDevice>();
		var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
		InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, rightHandedControllers);


		foreach (var device in rightHandedControllers)
		{
			Debug.Log(string.Format("Device name '{0}' has characteristics '{1}'", device.name, device.characteristics.ToString()));
			XRRightController = device;
		}

	}

	#region MRTunneling parameters related
	// change mid foveate radius
	public void ChangeMidFoveatRadius(SliderEventData sliderEventData)
	{
		LeftEye.SetFloat("_MidFoveateRadius", sliderEventData.NewValue*10);
		RightEye.SetFloat("_MidFoveateRadius", sliderEventData.NewValue*10);
	}

	// change central foveate radius
	public void ChangCentralFoveateRadius(SliderEventData eventData)
	{
		LeftEye. SetFloat("_CentralRadius", eventData.NewValue/2);
		RightEye.SetFloat("_CentralRadius", eventData.NewValue/2);
	}
	#endregion


	#region HSVC

	public void OnBrightnessValueChange(SliderEventData eventData)
	{
		brightness_val = Mathf.Lerp(0, 1.5f, eventData.NewValue);

		updateHSVCValue(LeftEye);
		updateHSVCValue(RightEye);
	}

	public void OnHueValueChange(SliderEventData eventData)
	{
		hue_val = Mathf.Lerp(-100, 100, eventData.NewValue);
		updateHSVCValue(LeftEye);
		updateHSVCValue(RightEye);
	}

	public void OnContrastValueChange(SliderEventData eventData)
	{
		contrast_val = Mathf.Lerp(-0.5f, 2.2f, eventData.NewValue);
		updateHSVCValue(LeftEye);
		updateHSVCValue(RightEye);
	}


	public void OnSaturationValueChange(SliderEventData eventData)
	{
		saturation_val = Mathf.Lerp(-7, 7, eventData.NewValue) ;
		updateHSVCValue(LeftEye);
		updateHSVCValue(RightEye);
	}
	private float normalize_float01(float val, float min, float max)
    {
		return (val - min) / (max - min);
    }
	private void updateHSVCValue(Material mat)
	{
		mat.EnableKeyword("EFFECT_HSVC");
		mat.DisableKeyword("EFFECT_KERNEL_MATRIX");

		m_HueSatValCont = mat.GetVector("_HSVC");
		//m_FoldHSVCMatrix = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldHSVCMatrix, "Hue Saturation Value Contrast SETUP");

		//m_PixelDistMulAdd.x = PixelWidthSlider.value;
		m_PixelDistMulAdd.x = 1;
		m_PixelDistMulAdd.y = m_PixelDistMulAdd.x;

		m_PixelDistMulAdd.z = 1.0f;//EditorGUILayout.FloatField("Effect Multiply", m_PixelDistMulAdd.z);
		m_PixelDistMulAdd.w = 0f; // EditorGUILayout.FloatField("Effect Add", m_PixelDistMulAdd.w);

		m_HueSatValCont.x = hue_val;//EditorGUILayout.FloatField("Hue", m_HueSatValCont.x);
		m_HueSatValCont.y = saturation_val;//EditorGUILayout.FloatField("Saturation", m_HueSatValCont.y);
		m_HueSatValCont.z = brightness_val;//EditorGUILayout.FloatField("Value (Brightness)", m_HueSatValCont.z);
		m_HueSatValCont.w = contrast_val;//EditorGUILayout.FloatField("Contrast", m_HueSatValCont.w);

		//Undo.RegisterCompleteObjectUndo(m_Mat, "ImageEffectFilter");
		Set_HSVCMatrix(m_HueSatValCont, ref m_HueMatrix, ref m_SVCMatrix);
		m_Color4x4 = m_HueMatrix * m_SVCMatrix;
		mat.SetVector("_HSVC", m_HueSatValCont);

		mat.SetVector("_C0", m_Color4x4.GetRow(0));
		mat.SetVector("_C1", m_Color4x4.GetRow(1));
		mat.SetVector("_C2", m_Color4x4.GetRow(2));
		mat.SetVector("_C3", m_Color4x4.GetRow(3));
		mat.SetVector("_PixDistMulAdd", m_PixelDistMulAdd);


	}

	private void setHSVCValueOriginal(Material mat)
	{
		mat.EnableKeyword("EFFECT_HSVC");
		mat.DisableKeyword("EFFECT_KERNEL_MATRIX");

		m_HueSatValCont = mat.GetVector("_HSVC");
	
		//m_FoldHSVCMatrix = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldHSVCMatrix, "Hue Saturation Value Contrast SETUP");

		//m_PixelDistMulAdd.x = PixelWidthSlider.value;
		m_PixelDistMulAdd.x = 1;
		m_PixelDistMulAdd.y = m_PixelDistMulAdd.x;

		m_PixelDistMulAdd.z = 1.0f;//EditorGUILayout.FloatField("Effect Multiply", m_PixelDistMulAdd.z);
		m_PixelDistMulAdd.w = 0f; // EditorGUILayout.FloatField("Effect Add", m_PixelDistMulAdd.w);

		m_HueSatValCont.x = 0; // HueSlider.value;//EditorGUILayout.FloatField("Hue", m_HueSatValCont.x);
		m_HueSatValCont.y = 1; // SaturationSlider.value;//EditorGUILayout.FloatField("Saturation", m_HueSatValCont.y);
		m_HueSatValCont.z = 1; // BrightnessSlider.value;//EditorGUILayout.FloatField("Value (Brightness)", m_HueSatValCont.z);
		m_HueSatValCont.w = 1; // ContrastSlider.value;//EditorGUILayout.FloatField("Contrast", m_HueSatValCont.w);

		BrightnessSlider.SliderValue = normalize_float01(1, 0, 1.5f);
		HueSlider.SliderValue        = normalize_float01(0, -100, 100);
		ContrastSlider.SliderValue   = normalize_float01(1, -0.5f, 2.2f);
		SaturationSlider.SliderValue = normalize_float01(1, -7.0f, 7.0f);

		//Undo.RegisterCompleteObjectUndo(m_Mat, "ImageEffectFilter");
		Set_HSVCMatrix(m_HueSatValCont, ref m_HueMatrix, ref m_SVCMatrix);
		m_Color4x4 = m_HueMatrix * m_SVCMatrix;
		mat.SetVector("_HSVC", m_HueSatValCont);

		mat.SetVector("_C0", m_Color4x4.GetRow(0));
		mat.SetVector("_C1", m_Color4x4.GetRow(1));
		mat.SetVector("_C2", m_Color4x4.GetRow(2));
		mat.SetVector("_C3", m_Color4x4.GetRow(3));
		mat.SetVector("_PixDistMulAdd", m_PixelDistMulAdd);
	}
	private void Set_HSVCMatrix(Vector4 a_HueSatValCont, ref Matrix4x4 a_HueMatrix, ref Matrix4x4 a_SVCMatrix)
	{
		var l_Hue = a_HueSatValCont.x;
		var l_Saturation = a_HueSatValCont.y;
		var l_Brightness = a_HueSatValCont.z; // Also known as Value as 'V' in (HSV)
		var l_Contrast = a_HueSatValCont.w;

		float l_Value = (1 - l_Contrast) / 2 + (l_Brightness - 1); // Use 1 for Normal Brightness
		float l_SatR = (1 - l_Saturation) * m_Luminance.x;
		float l_SatG = (1 - l_Saturation) * m_Luminance.y;
		float l_SatB = (1 - l_Saturation) * m_Luminance.z;

		a_SVCMatrix = Matrix4x4.identity;
		a_SVCMatrix.SetRow(0, new Vector4(l_Contrast * (l_SatR + l_Saturation), l_Contrast * l_SatR, l_Contrast * l_SatR, 0));
		a_SVCMatrix.SetRow(1, new Vector4(l_Contrast * l_SatG, l_Contrast * (l_SatG + l_Saturation), l_Contrast * l_SatG, 0));
		a_SVCMatrix.SetRow(2, new Vector4(l_Contrast * l_SatB, l_Contrast * l_SatB, l_Contrast * (l_SatB + l_Saturation), 0));
		a_SVCMatrix.SetRow(3, new Vector4(l_Value, l_Value, l_Value, 1));

		a_HueMatrix = Matrix4x4.identity;
		float l_Cos = Mathf.Cos(l_Hue * Mathf.Deg2Rad);
		float l_Sin = Mathf.Sin(l_Hue * Mathf.Deg2Rad);
		float l_C1 = 0.33333f * (1.0f - l_Cos); // (1/3f) = 0.33333;
		float l_C2 = 0.57735f * l_Sin; // Sqrt(1/3) = 0.57735;
		a_HueMatrix.m00 = a_HueMatrix.m11 = a_HueMatrix.m22 = l_Cos + l_C1;
		a_HueMatrix.m01 = a_HueMatrix.m12 = a_HueMatrix.m20 = l_C1 - l_C2;
		a_HueMatrix.m02 = a_HueMatrix.m10 = a_HueMatrix.m21 = l_C1 + l_C2;
	}
	#endregion

	#region image effects
	public void SetOrigin()
	{
		m_KernelMatrix = KernelMatrix.Original;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);

		setHSVCValueOriginal(LeftEye);
		setHSVCValueOriginal(RightEye);
	}

	public void SetSharp()
	{
		m_KernelMatrix = KernelMatrix.Sharp;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);
	}

	public void SetEdge()
	{
		m_KernelMatrix = KernelMatrix.Edge;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);
	}

	public void SetEmboss()
	{

		m_KernelMatrix = KernelMatrix.Emboss;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);
	}

	public void SetGaussianBlur()
	{
		m_KernelMatrix = KernelMatrix.GaussianBlur;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);
	}

	public void SetBoxBlur()
	{

		m_KernelMatrix = KernelMatrix.BoxBlur;
		SetNewEffect(LeftEye);
		SetNewEffect(RightEye);
	}
	void SetNewEffect(Material m_Mat)
	{
		m_Mat.DisableKeyword("EFFECT_HSVC");
		m_Mat.EnableKeyword("EFFECT_KERNEL_MATRIX");
		m_KMul = m_Mat.GetFloat("_KMul");
		Vector4 l_K0123 = m_Mat.GetVector("_0123");
		Vector4 l_K4567 = m_Mat.GetVector("_4567");
		Vector4 l_K89AB = m_Mat.GetVector("_89AB");
		if (m_Kernel3x3 == null) m_Kernel3x3 = new float[9];
		m_Kernel3x3[0] = l_K0123.x;
		m_Kernel3x3[1] = l_K0123.y;
		m_Kernel3x3[2] = l_K0123.z;
		m_Kernel3x3[3] = l_K0123.w;
		m_Kernel3x3[4] = l_K4567.x;
		m_Kernel3x3[5] = l_K4567.y;
		m_Kernel3x3[6] = l_K4567.z;
		m_Kernel3x3[7] = l_K4567.w;
		m_Kernel3x3[8] = l_K89AB.x;


		Set_KernelPreset(m_KernelMatrix, ref m_Kernel3x3, ref m_KMul);

		l_K0123.x = m_Kernel3x3[0];
		l_K0123.y = m_Kernel3x3[1];
		l_K0123.z = m_Kernel3x3[2];
		l_K0123.w = m_Kernel3x3[3];
		l_K4567.x = m_Kernel3x3[4];
		l_K4567.y = m_Kernel3x3[5];
		l_K4567.z = m_Kernel3x3[6];
		l_K4567.w = m_Kernel3x3[7];
		l_K89AB.x = m_Kernel3x3[8];
		m_Mat.SetVector("_0123", l_K0123);
		m_Mat.SetVector("_4567", l_K4567);
		m_Mat.SetVector("_89AB", l_K89AB);
		m_Mat.SetFloat("_KMul", m_KMul);
		m_KMul = 1;// EditorGUILayout.FloatField("Kernel Multiplier", m_KMul);
		m_Mat.SetVector("_PixDistMulAdd", m_PixelDistMulAdd);

	}

	public void OnPixelWidthValueChange()
	{
		//m_PixelDistMulAdd.x = PixelWidthSlider.value;//EditorGUILayout.FloatField("Pixel Distance", m_PixelDistMulAdd.x);
		m_PixelDistMulAdd.z = 1.0f;  //EditorGUILayout.FloatField("Effect Multiply", m_PixelDistMulAdd.z);
		m_PixelDistMulAdd.w = 0; // EditorGUILayout.FloatField("Effect Add", m_PixelDistMulAdd.w);
		m_PixelDistMulAdd.y = m_PixelDistMulAdd.x;
		LeftEye.SetVector("_PixDistMulAdd", m_PixelDistMulAdd);
		RightEye.SetVector("_PixDistMulAdd", m_PixelDistMulAdd);

	}
	#endregion
	void Set_KernelPreset(KernelMatrix a_Type, ref float[] a_Kernel, ref float a_KMul)
	{
		a_KMul = 1;
		switch (a_Type)
		{
			case KernelMatrix.Original:
				a_Kernel = new float[] { 0, 0, 0, 0, 1, 0, 0, 0, 0 };
				m_PixelDistMulAdd.x = 1;
				break;
			case KernelMatrix.Sharp:
				a_Kernel = new float[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 };
				m_PixelDistMulAdd.x = 2;
				break;
			case KernelMatrix.Edge:
				a_Kernel = new float[] { -1, -1, -1, -1, 8, -1, -1, -1, -1 };
				m_PixelDistMulAdd.x = 2;
				break;
			case KernelMatrix.Emboss:
				a_Kernel = new float[] { -2, -1, 0, -1, 0, 1, 0, 1, 2 };
				m_PixelDistMulAdd.x = 5;
				break;
			case KernelMatrix.BoxBlur:
				a_Kernel = new float[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
				a_KMul = 1 / 9f;
				m_PixelDistMulAdd.x = 5;
				break;
			case KernelMatrix.GaussianBlur:
				a_Kernel = new float[] { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
				a_KMul = 1 / 16f;
				m_PixelDistMulAdd.x = 5;
				break;
			default:
				break;
		}
	}
}
