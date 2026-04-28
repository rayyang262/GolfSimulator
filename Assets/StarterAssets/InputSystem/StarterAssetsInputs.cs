using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool togglePhone;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			// Jump disabled — spacebar is reserved for swing
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnTogglePhone(InputValue value)
		{
			togglePhone = value.isPressed;
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		private PhoneAnimator _phoneAnimator;

		private void Update()
		{
			if (_phoneAnimator == null)
				_phoneAnimator = FindObjectOfType<PhoneAnimator>();

			if (_phoneAnimator != null && _phoneAnimator.IsPhoneUp)
			{
				// Phone is up: unlock cursor, stop camera look
				if (Cursor.lockState != CursorLockMode.None)
				{
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}
				cursorInputForLook = false;
				look = Vector2.zero; // prevent residual camera drift
			}
			else
			{
				// Phone is down: lock cursor, enable camera look
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
				cursorInputForLook = true;
			}
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			// When regaining focus, only lock cursor if phone is not up
			if (hasFocus && (_phoneAnimator == null || !_phoneAnimator.IsPhoneUp))
				SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}