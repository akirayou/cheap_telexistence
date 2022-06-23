#include "SPDMotor.h"
#define PWM_LIMIT 150
SPDMotor::SPDMotor( int encoderA, int encoderB, bool encoderReversed, int motorPWM, int motorDir1, int motorDir2 ,float speedMul,unsigned long PID_span) {
  _encoder = new Encoder(encoderA, encoderB);
  _encoderReversed = encoderReversed;
  
  _motorPWM = motorPWM;
  pinMode( _motorPWM, OUTPUT );
  _motorDir1 = motorDir1;
  pinMode( _motorDir1, OUTPUT );
  _motorDir2 = motorDir2;
  pinMode( _motorDir2, OUTPUT );
  _speedMul=speedMul;
  _oldUtime=micros();
  _oldEncoder=getEncoderPosition();
  _eSpeed=0;
  _PID_span=PID_span;

}


void SPDMotor::kick(){
  static Tmicro pid_elasp;
  Tmicro now=micros();
  Tmicro diffT=now-_oldUtime;
  long enc=getEncoderPosition();
  
  _eSpeed= 0.9f*_eSpeed + 0.1f* ((enc-_oldEncoder) * _speedMul  )/diffT;
  _oldUtime=now;
  _oldEncoder=enc;
  pid_elasp+=diffT;
  if(!_eMode ||  pid_elasp<_PID_span)return;
  pid_elasp-=_PID_span;
  if(_PID_span<pid_elasp)pid_elasp=0; //行き過ぎなのでリセット

  //PIDmode
  float diff = _targetESpeed - _eSpeed ;
  D=diff-P;
  P=diff;
  I+=diff;
  I*=0.99f; //停止時にpwm音がひびかなように
  I=constrain(I,-1e+2f,1e+2f);
  
  speed(constrain(k.P*P+k.I*I+k.D*D,-PWM_LIMIT,PWM_LIMIT));
}


void SPDMotor::speed( int speedPWM ) {
  _speed = speedPWM;
  if( speedPWM == 0 ) {
    digitalWrite(_motorDir1,LOW);
    digitalWrite(_motorDir2,LOW);
    analogWrite( _motorPWM, 255);
  } else if( speedPWM > 0 ) {
    digitalWrite(_motorDir1, LOW );
    digitalWrite(_motorDir2, HIGH );
    analogWrite( _motorPWM, speedPWM < PWM_LIMIT ? speedPWM : PWM_LIMIT);
  } else if( speedPWM < 0 ) {
    digitalWrite(_motorDir1, HIGH );
    digitalWrite(_motorDir2, LOW );
    analogWrite( _motorPWM, (-speedPWM) < PWM_LIMIT ? (-speedPWM): PWM_LIMIT);
  }
}

/// Activate a SHORT BRAKE mode, which shorts the motor drive EM, clamping motion.
void SPDMotor::hardStop() {
    _speed = 0;
    digitalWrite(_motorDir1,HIGH);
    digitalWrite(_motorDir2,HIGH);
    analogWrite( _motorPWM, 0);
}

/// Get the current speed.
int SPDMotor::getSpeed() {
    return _speed;
}

/// Get the current rotation position from the encoder.
long SPDMotor::getEncoderPosition() {
  long position = _encoder->read();
  return _encoderReversed ? -position : position;
}
