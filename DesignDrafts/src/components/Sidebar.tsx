import React from 'react';
import { useApp } from '../context/AppContext';
import { NavTab } from '../types';
import {
  LayoutDashboard,
  Users,
  Calendar,
  FileText,
  Package,
  TrendingUp,
  HelpCircle,
  Settings,
  Plus,
  Cross,
  Sparkles,
} from 'lucide-react';

export const Sidebar: React.FC = () => {
  const { lang, activeTab, setActiveTab, setIsApptModalOpen } = useApp();
  const isAr = lang === 'ar';

  const navItems: { id: NavTab; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
    {
      id: 'dashboard',
      label: isAr ? 'لوحة التحكم' : 'Dashboard',
      icon: LayoutDashboard,
    },
    {
      id: 'patients',
      label: isAr ? 'المرضى' : 'Patients',
      icon: Users,
    },
    {
      id: 'schedules',
      label: isAr ? 'الجداول' : 'Schedules',
      icon: Calendar,
    },
    {
      id: 'records',
      label: isAr ? 'السجلات الطبية' : 'Medical Records',
      icon: FileText,
    },
    {
      id: 'inventory',
      label: isAr ? 'المخزون' : 'Inventory',
      icon: Package,
    },
    {
      id: 'analytics',
      label: isAr ? 'التحليلات' : 'Analytics',
      icon: TrendingUp,
    },
  ];

  return (
    <aside
      id="main-sidebar"
      className="hidden md:flex flex-col h-screen sticky top-0 overflow-y-auto w-[240px] bg-white border-e border-[#E2E8F0] py-4 gap-1 shrink-0 z-40"
    >
      {/* Brand Header */}
      <div className="px-4 mb-4">
        <div className="flex items-center gap-2.5 mb-3.5">
          <div className="w-9 h-9 rounded-lg bg-[#006194]/10 border border-[#006194]/20 flex items-center justify-center text-[#006194]">
            <span className="material-symbols-outlined text-[20px] text-[#006194]">local_hospital</span>
          </div>
          <div className="min-w-0">
            <h1 className="text-[17px] font-bold text-[#006194] leading-tight truncate">
              {isAr ? 'المركز الطبي' : 'Medical Center'}
            </h1>
            <span className="text-[11px] font-medium text-[#64748B] block">
              {isAr ? 'فئة ممتازة' : 'Premium Tier'}
            </span>
          </div>
        </div>

        {/* Action Button */}
        <button
          id="btn-new-appointment"
          onClick={() => setIsApptModalOpen(true)}
          className="w-full h-10 bg-[#006194] hover:bg-[#004b73] text-white font-medium text-[13px] rounded-md transition-all flex items-center justify-center gap-1.5 shadow-sm active:scale-[0.98] cursor-pointer"
        >
          <Plus className="w-4 h-4" />
          <span>{isAr ? 'موعد جديد' : 'New Appointment'}</span>
        </button>
      </div>

      {/* Nav items */}
      <nav className="flex-1 px-2 flex flex-col gap-1">
        {navItems.map((item) => {
          const Icon = item.icon;
          const isActive = activeTab === item.id;
          return (
            <button
              key={item.id}
              id={`nav-item-${item.id}`}
              onClick={() => setActiveTab(item.id)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-md text-[13px] font-medium transition-all text-start cursor-pointer ${
                isActive
                  ? 'bg-[#eaedff] text-[#006194] border-s-4 border-[#006194] font-semibold'
                  : 'text-[#64748B] hover:text-[#0F172A] hover:bg-[#f2f3ff] border-s-4 border-transparent'
              }`}
            >
              <Icon className={`w-[18px] h-[18px] shrink-0 ${isActive ? 'text-[#006194]' : 'text-[#64748B]'}`} />
              <span className="truncate">{item.label}</span>
            </button>
          );
        })}
      </nav>

      {/* Bottom Nav items */}
      <div className="px-2 mt-auto flex flex-col gap-1 border-t border-[#E2E8F0] pt-2">
        <button
          id="nav-item-support"
          onClick={() => setActiveTab('support')}
          className={`flex items-center gap-3 px-3 py-2 rounded-md text-[13px] font-medium transition-all text-start cursor-pointer ${
            activeTab === 'support'
              ? 'bg-[#eaedff] text-[#006194] border-s-4 border-[#006194]'
              : 'text-[#64748B] hover:text-[#0F172A] hover:bg-[#f2f3ff] border-s-4 border-transparent'
          }`}
        >
          <HelpCircle className="w-[18px] h-[18px] shrink-0 text-[#64748B]" />
          <span>{isAr ? 'الدعم' : 'Support'}</span>
        </button>

        <button
          id="nav-item-settings"
          onClick={() => setActiveTab('settings')}
          className={`flex items-center gap-3 px-3 py-2 rounded-md text-[13px] font-medium transition-all text-start cursor-pointer ${
            activeTab === 'settings'
              ? 'bg-[#eaedff] text-[#006194] border-s-4 border-[#006194] font-semibold'
              : 'text-[#64748B] hover:text-[#0F172A] hover:bg-[#f2f3ff] border-s-4 border-transparent'
          }`}
        >
          <Settings className="w-[18px] h-[18px] shrink-0 text-[#64748B]" />
          <span>{isAr ? 'الإعدادات' : 'Settings'}</span>
        </button>
      </div>
    </aside>
  );
};
