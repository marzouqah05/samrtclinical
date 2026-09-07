import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { Role } from '../types';
import { Eye, EyeOff, Languages, Shield, Stethoscope, HeartPulse } from 'lucide-react';

export const AuthScreen: React.FC = () => {
  const { lang, toggleLang, login } = useApp();
  const isAr = lang === 'ar';

  const [selectedRole, setSelectedRole] = useState<Role>('admin');
  const [email, setEmail] = useState('dr.smith@clinic.com');
  const [password, setPassword] = useState('password123');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(true);

  const handleRoleChange = (role: Role) => {
    setSelectedRole(role);
    if (role === 'admin') {
      setEmail('sarah@clinicflow.com');
    } else if (role === 'doctor') {
      setEmail('dr.smith@clinic.com');
    } else {
      setEmail('nurse.emily@clinic.com');
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    login(selectedRole, email);
  };

  return (
    <div className="h-screen w-full bg-[#F8FAFC] text-[#0F172A] flex flex-col lg:flex-row overflow-y-auto">
      {/* Editorial Branding Section */}
      <div
        id="auth-branding-section"
        className="hidden lg:flex w-1/2 bg-[#006a63] text-white flex-col justify-between p-10 xl:p-12 relative overflow-hidden shrink-0"
      >
        {/* Subtle Grid Background */}
        <div
          className="absolute inset-0 z-0 pointer-events-none opacity-20"
          style={{
            backgroundImage:
              'linear-gradient(to right, rgba(255, 255, 255, 0.15) 1px, transparent 1px), linear-gradient(to bottom, rgba(255, 255, 255, 0.15) 1px, transparent 1px)',
            backgroundSize: '40px 40px',
          }}
        />

        {/* Top Branding Logo */}
        <div className="z-10 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <span className="material-symbols-outlined text-[28px] text-[#9cf2e8]">
              medical_services
            </span>
            <h1 className="text-[28px] font-bold tracking-tight text-white">ClinicFlow</h1>
          </div>
        </div>

        {/* Center Content */}
        <div className="z-10 max-w-lg my-auto py-8">
          <h2 className="text-[34px] xl:text-[38px] font-bold mb-4 leading-tight">
            {isAr
              ? 'نظام التشغيل الذكي للعيادات الحديثة.'
              : 'The intelligent operating system for modern clinics.'}
          </h2>
          <p className="text-[16px] text-[#9cf2e8] opacity-90 leading-relaxed mb-8">
            {isAr
              ? 'قم بتبسيط رعاية المرضى، وأتمتة الإدارة، واكتشاف رؤى عميقة من خلال منصتنا الشاملة للرعاية الصحية.'
              : 'Streamline patient care, automate administration, and unlock deep insights with our comprehensive B2B healthcare platform.'}
          </p>

          {/* Stats Bar */}
          <div className="flex gap-8 pt-6 border-t border-[#00504a]">
            <div>
              <div className="text-[26px] font-bold text-[#9cf2e8]" dir="ltr">99.9%</div>
              <div className="text-[12px] uppercase tracking-wider text-[#80d5cb] font-medium">
                {isAr ? 'ضمان وقت التشغيل' : 'Uptime Guarantee'}
              </div>
            </div>
            <div>
              <div className="text-[26px] font-bold text-[#9cf2e8]" dir="ltr">10k+</div>
              <div className="text-[12px] uppercase tracking-wider text-[#80d5cb] font-medium">
                {isAr ? 'عيادات موثوقة' : 'Clinics Trusted'}
              </div>
            </div>
          </div>
        </div>

        {/* Bottom Ambient Glow */}
        <div className="absolute -bottom-32 -right-32 w-[500px] h-[500px] bg-[#80d5cb] rounded-full blur-[140px] opacity-20 pointer-events-none z-0" />
      </div>

      {/* Login Area */}
      <div
        id="auth-form-section"
        className="w-full lg:w-1/2 flex flex-col justify-center items-center p-6 md:p-12 relative min-h-screen"
      >
        {/* Language Switch Button */}
        <div className="absolute top-6 end-6 flex items-center gap-2">
          <button
            id="auth-toggle-lang"
            onClick={toggleLang}
            className="text-[13px] font-medium text-[#64748B] hover:text-[#0F172A] transition-colors flex items-center gap-1.5 px-3 py-1.5 border border-[#E2E8F0] rounded-md bg-white hover:bg-[#F8FAFC] shadow-2xs cursor-pointer"
          >
            <Languages className="w-4 h-4 text-[#006194]" />
            <span>{isAr ? 'English' : 'عربي'}</span>
          </button>
        </div>

        {/* Mobile Header */}
        <div className="lg:hidden flex items-center gap-2 mb-8 mt-6">
          <span className="material-symbols-outlined text-[28px] text-[#006a63]">
            medical_services
          </span>
          <h1 className="text-[26px] font-bold text-[#0F172A]">ClinicFlow</h1>
        </div>

        <div className="w-full max-w-[420px]">
          <div className="mb-6 text-center lg:text-start">
            <h2 className="text-[26px] lg:text-[30px] font-bold text-[#0F172A] mb-1.5">
              {isAr ? 'مرحباً بك مجدداً' : 'Welcome back'}
            </h2>
            <p className="text-[14px] text-[#64748B]">
              {isAr
                ? 'يرجى إدخال بياناتك للوصول إلى لوحة التحكم الخاصة بك.'
                : 'Please enter your details to access your dashboard.'}
            </p>
          </div>

          {/* Login Form Card */}
          <div className="bg-white border border-[#E2E8F0] rounded-lg p-6 shadow-xs">
            {/* Role Selector */}
            <div className="mb-5">
              <label className="text-[11px] font-semibold text-[#64748B] block mb-2 uppercase tracking-wider">
                {isAr ? 'اختر الدور' : 'Select Role'}
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                <button
                  type="button"
                  id="role-admin-btn"
                  onClick={() => handleRoleChange('admin')}
                  className={`py-2 text-[13px] font-medium rounded transition-all cursor-pointer ${
                    selectedRole === 'admin'
                      ? 'border border-[#006194] bg-[#eaedff] text-[#006194] font-bold shadow-2xs'
                      : 'border border-[#E2E8F0] text-[#64748B] hover:text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  {isAr ? 'مسؤول' : 'Admin'}
                </button>
                <button
                  type="button"
                  id="role-doctor-btn"
                  onClick={() => handleRoleChange('doctor')}
                  className={`py-2 text-[13px] font-medium rounded transition-all cursor-pointer ${
                    selectedRole === 'doctor'
                      ? 'border border-[#006194] bg-[#eaedff] text-[#006194] font-bold shadow-2xs'
                      : 'border border-[#E2E8F0] text-[#64748B] hover:text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  {isAr ? 'طبيب' : 'Doctor'}
                </button>
                <button
                  type="button"
                  id="role-nurse-btn"
                  onClick={() => handleRoleChange('nurse')}
                  className={`py-2 text-[13px] font-medium rounded transition-all cursor-pointer ${
                    selectedRole === 'nurse'
                      ? 'border border-[#006194] bg-[#eaedff] text-[#006194] font-bold shadow-2xs'
                      : 'border border-[#E2E8F0] text-[#64748B] hover:text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  {isAr ? 'ممرض' : 'Nurse'}
                </button>
              </div>
            </div>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
              {/* Email Input */}
              <div>
                <label
                  htmlFor="auth-email"
                  className="text-[13px] font-medium text-[#0F172A] block mb-1"
                >
                  {isAr ? 'البريد الإلكتروني المهني' : 'Professional Email'}
                </label>
                <input
                  id="auth-email"
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="dr.smith@clinic.com"
                  dir="ltr"
                  className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[14px] text-[#0F172A] focus:outline-none focus:border-[#006194] focus:ring-1 focus:ring-[#006194] transition-shadow placeholder:text-[#64748B]/50 text-start"
                />
              </div>

              {/* Password Input */}
              <div>
                <div className="flex justify-between items-center mb-1">
                  <label
                    htmlFor="auth-password"
                    className="text-[13px] font-medium text-[#0F172A] block"
                  >
                    {isAr ? 'كلمة المرور' : 'Password'}
                  </label>
                  <a
                    href="#"
                    onClick={(e) => e.preventDefault()}
                    className="text-[12px] text-[#006194] hover:underline font-medium"
                  >
                    {isAr ? 'نسيت كلمة المرور؟' : 'Forgot password?'}
                  </a>
                </div>
                <div className="relative">
                  <input
                    id="auth-password"
                    type={showPassword ? 'text' : 'password'}
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••"
                    dir="ltr"
                    className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[14px] text-[#0F172A] focus:outline-none focus:border-[#006194] focus:ring-1 focus:ring-[#006194] transition-shadow pe-10"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute end-2.5 top-1/2 -translate-y-1/2 text-[#64748B] hover:text-[#0F172A] p-1 cursor-pointer"
                  >
                    {showPassword ? (
                      <EyeOff className="w-4 h-4" />
                    ) : (
                      <Eye className="w-4 h-4" />
                    )}
                  </button>
                </div>
              </div>

              {/* Remember Me */}
              <div className="flex items-center gap-2 mt-1">
                <input
                  id="auth-remember"
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="rounded border-[#CBD5E1] text-[#006a63] focus:ring-[#006a63] w-4 h-4 cursor-pointer"
                />
                <label
                  htmlFor="auth-remember"
                  className="text-[13px] text-[#64748B] cursor-pointer select-none"
                >
                  {isAr ? 'تذكرني لمدة 30 يوماً' : 'Remember me for 30 days'}
                </label>
              </div>

              {/* Submit Button */}
              <button
                type="submit"
                id="btn-auth-submit"
                className="w-full bg-[#006a63] hover:bg-[#00504a] text-white font-medium text-[14px] py-2.5 rounded mt-2 transition-colors flex items-center justify-center gap-2 cursor-pointer shadow-sm active:scale-[0.99]"
              >
                <span>{isAr ? 'تسجيل الدخول بأمان' : 'Sign In securely'}</span>
              </button>
            </form>
          </div>

          <p className="mt-6 text-center text-[13px] text-[#64748B]">
            {isAr ? 'ليس لديك حساب؟ ' : "Don't have an account? "}
            <a
              href="#"
              onClick={(e) => e.preventDefault()}
              className="text-[#006194] font-medium hover:underline"
            >
              {isAr ? 'تواصل مع المبيعات' : 'Contact Sales'}
            </a>
          </p>
        </div>
      </div>
    </div>
  );
};
