#ifndef SPDMOTOR_H_
#define SPDMOTOR_H_

#include <Encoder.h>
// --- SPD Motor ---
class SPDMotor {
  private:
    typedef  unsigned long Tmicro;
    Encoder *_encoder;
    bool _encoderReversed,_eMode;
    int _motorPWM, _motorDir1, _motorDir2;
    // Current speed setting.
    int _speed;
    //speed with encoder
    Tmicro _oldUtime; 
    float _eSpeed,_targetESpeed;
    long _oldEncoder;
    float _speedMul;
    unsigned long _PID_span;
  public:
  struct {
      float P;
      float I;
      float D;
  } k;
  float P,I,D;
  SPDMotor( int encoderA, int encoderB, bool encoderReversed, int motorPWM, int motorDir1, int motorDir2,float speedMul=1e+6f/2500.0f,unsigned long PID_span=100000L);
  void speed( int pwm );//-255 to 255
  void eSpeed(float tar){_targetESpeed=tar;} //normaly -1 to 1 normalized by speedMul
  /// Activate a SHORT BRAKE mode, which shorts the motor drive EM, clamping motion.
  void hardStop();
  /// Get the current speed.
  int getSpeed();
  /// Get the current rotation position from the encoder.
  long getEncoderPosition();
  
  void kick();
  float getESpeed(){return _eSpeed;}
  void setEmode(bool enable){
    if( (!_eMode) && enable ){
      P=0;
      I=0;
      D=0;
      _oldEncoder=getEncoderPosition();
      _eSpeed=_targetESpeed=0;
    }
    _eMode=enable;
  }
};
#endif