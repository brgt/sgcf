import{d as A,g as _,c as n,b as g,p as I,r as O,l as $,T as k,o as r,f as w,a as D,n as h,F as V,e as N,t as C}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as E}from"./_plugin-vue_export-helper-DlAUqK2U.js";const j={class:"nw-dropdown"},T=["onClick"],S=A({__name:"Dropdown",props:{items:{},placement:{default:"bottom-start"}},emits:["select"],setup(t,{emit:a}){const e=a,s=_(!1),y=c=>{c.disabled||(e("select",c),s.value=!1)};return(c,i)=>(r(),n("div",j,[g("div",{onClick:i[0]||(i[0]=o=>s.value=!s.value)},[O(c.$slots,"trigger",{},()=>[i[1]||(i[1]=g("button",{class:"nw-dropdown__trigger"},[w(" Options "),g("span",{class:"i-carbon-chevron-down"})],-1))],!0)]),I(k,{name:"dropdown"},{default:$(()=>[s.value?(r(),n("div",{key:0,class:h(["nw-dropdown__menu",`nw-dropdown__menu--${t.placement}`])},[(r(!0),n(V,null,N(t.items,(o,x)=>(r(),n("button",{key:x,class:h(["nw-dropdown__item",{"nw-dropdown__item--disabled":o.disabled,"nw-dropdown__item--divider":o.divider}]),onClick:B=>y(o)},[o.icon?(r(),n("span",{key:0,class:h(["nw-dropdown__icon",o.icon])},null,2)):D("",!0),w(" "+C(o.label),1)],10,T))),128))],2)):D("",!0)]),_:1})]))}}),l=E(S,[["__scopeId","data-v-297d0140"]]);S.__docgenInfo={exportName:"default",displayName:"Dropdown",description:"",tags:{},props:[{name:"items",required:!0,type:{name:"Array",elements:[{name:"DropdownItem"}]}},{name:"placement",required:!1,type:{name:"union",elements:[{name:'"bottom-start"'},{name:'"bottom-end"'}]},defaultValue:{func:!1,value:"'bottom-start'"}}],events:[{name:"select",type:{names:["DropdownItem"]}}],slots:[{name:"trigger"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Dropdown/Dropdown.vue"]};const F={title:"Design System/Molecules/Dropdown",component:l,tags:["autodocs"],argTypes:{items:{control:"object",description:"Array de itens do dropdown"},placement:{control:"select",options:["bottom-start","bottom-end"],description:"Posicionamento do dropdown"}},args:{items:[{label:"Option 1",value:"1"},{label:"Option 2",value:"2"},{label:"Option 3",value:"3"}],placement:"bottom-start"}},f=[{label:"Edit",value:"edit",icon:"i-carbon-edit"},{label:"Duplicate",value:"duplicate",icon:"i-carbon-copy"},{label:"Archive",value:"archive",icon:"i-carbon-archive"},{label:"Delete",value:"delete",icon:"i-carbon-trash-can",divider:!0}],d={render:t=>({components:{Dropdown:l},setup(){return{args:t,handleSelect:e=>{console.log("Selected:",e)}}},template:`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-primary text-white rounded-lg">
              Actions
            </button>
          </template>
        </Dropdown>
      </div>
    `}),args:{items:f,placement:"bottom-start"}},p={render:t=>({components:{Dropdown:l},setup(){return{args:t,handleSelect:e=>{alert(`Selected: ${e.label}`)}}},template:`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-gray-200 rounded-lg flex items-center gap-2">
              More Actions
              <span class="i-carbon-chevron-down"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    `}),args:{items:[{label:"View",value:"view",icon:"i-carbon-view"},{label:"Edit",value:"edit",icon:"i-carbon-edit"},{label:"Share",value:"share",icon:"i-carbon-share"},{label:"Download",value:"download",icon:"i-carbon-download",divider:!0},{label:"Delete",value:"delete",icon:"i-carbon-trash-can"}],placement:"bottom-start"}},m={render:t=>({components:{Dropdown:l},setup(){return{args:t,handleSelect:e=>{alert(`Selected: ${e.label}`)}}},template:`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-gray-200 rounded-lg">
              Options
            </button>
          </template>
        </Dropdown>
      </div>
    `}),args:{items:[{label:"Active Option",value:"1"},{label:"Disabled Option",value:"2",disabled:!0},{label:"Another Active",value:"3"}],placement:"bottom-start"}},u={render:t=>({components:{Dropdown:l},setup(){return{args:t,handleSelect:e=>{console.log("Selected:",e)}}},template:`
      <div class="p-8 flex justify-end">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="p-2 hover:bg-gray-100 rounded">
              <span class="i-carbon-overflow-menu-vertical text-xl"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    `}),args:{items:f,placement:"bottom-end"}},b={render:()=>({components:{Dropdown:l},setup(){return{items:[{label:"Profile",value:"profile",icon:"i-carbon-user"},{label:"Settings",value:"settings",icon:"i-carbon-settings"},{label:"Billing",value:"billing",icon:"i-carbon-receipt"},{label:"Sign Out",value:"signout",icon:"i-carbon-logout",divider:!0}],handleSelect:e=>{alert(`Selected: ${e.label}`)}}},template:`
      <div class="p-8 flex justify-end">
        <Dropdown :items="items" placement="bottom-end" @select="handleSelect">
          <template #trigger>
            <button class="flex items-center gap-2 p-2 hover:bg-gray-100 rounded-lg">
              <img
                src="https://i.pravatar.cc/40"
                alt="Avatar"
                class="w-8 h-8 rounded-full"
              />
              <span class="i-carbon-chevron-down"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    `})},v={render:()=>({components:{Dropdown:l},setup(){return{items:[{label:"View Details",value:"view",icon:"i-carbon-view"},{label:"Edit",value:"edit",icon:"i-carbon-edit"},{label:"Duplicate",value:"duplicate",icon:"i-carbon-copy"},{label:"Archive",value:"archive",icon:"i-carbon-archive",divider:!0},{label:"Delete",value:"delete",icon:"i-carbon-trash-can"}],handleSelect:e=>{alert(`Action: ${e.label}`)}}},template:`
      <div class="p-8">
        <table class="w-full">
          <thead>
            <tr class="border-b">
              <th class="text-left p-2">Name</th>
              <th class="text-left p-2">Status</th>
              <th class="text-right p-2">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="i in 3" :key="i" class="border-b">
              <td class="p-2">Item {{ i }}</td>
              <td class="p-2">Active</td>
              <td class="p-2 text-right">
                <Dropdown :items="items" placement="bottom-end" @select="handleSelect">
                  <template #trigger>
                    <button class="p-1 hover:bg-gray-100 rounded">
                      <span class="i-carbon-overflow-menu-vertical"></span>
                    </button>
                  </template>
                </Dropdown>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    `})};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Dropdown
    },
    setup() {
      const handleSelect = (item: DropdownItem) => {
        console.log('Selected:', item);
      };
      return {
        args,
        handleSelect
      };
    },
    template: \`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-primary text-white rounded-lg">
              Actions
            </button>
          </template>
        </Dropdown>
      </div>
    \`
  }),
  args: {
    items: defaultItems,
    placement: 'bottom-start'
  }
}`,...d.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Dropdown
    },
    setup() {
      const handleSelect = (item: DropdownItem) => {
        alert(\`Selected: \${item.label}\`);
      };
      return {
        args,
        handleSelect
      };
    },
    template: \`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-gray-200 rounded-lg flex items-center gap-2">
              More Actions
              <span class="i-carbon-chevron-down"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    \`
  }),
  args: {
    items: [{
      label: 'View',
      value: 'view',
      icon: 'i-carbon-view'
    }, {
      label: 'Edit',
      value: 'edit',
      icon: 'i-carbon-edit'
    }, {
      label: 'Share',
      value: 'share',
      icon: 'i-carbon-share'
    }, {
      label: 'Download',
      value: 'download',
      icon: 'i-carbon-download',
      divider: true
    }, {
      label: 'Delete',
      value: 'delete',
      icon: 'i-carbon-trash-can'
    }],
    placement: 'bottom-start'
  }
}`,...p.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Dropdown
    },
    setup() {
      const handleSelect = (item: DropdownItem) => {
        alert(\`Selected: \${item.label}\`);
      };
      return {
        args,
        handleSelect
      };
    },
    template: \`
      <div class="p-8">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="px-4 py-2 bg-gray-200 rounded-lg">
              Options
            </button>
          </template>
        </Dropdown>
      </div>
    \`
  }),
  args: {
    items: [{
      label: 'Active Option',
      value: '1'
    }, {
      label: 'Disabled Option',
      value: '2',
      disabled: true
    }, {
      label: 'Another Active',
      value: '3'
    }],
    placement: 'bottom-start'
  }
}`,...m.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Dropdown
    },
    setup() {
      const handleSelect = (item: DropdownItem) => {
        console.log('Selected:', item);
      };
      return {
        args,
        handleSelect
      };
    },
    template: \`
      <div class="p-8 flex justify-end">
        <Dropdown v-bind="args" @select="handleSelect">
          <template #trigger>
            <button class="p-2 hover:bg-gray-100 rounded">
              <span class="i-carbon-overflow-menu-vertical text-xl"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    \`
  }),
  args: {
    items: defaultItems,
    placement: 'bottom-end'
  }
}`,...u.parameters?.docs?.source}}};b.parameters={...b.parameters,docs:{...b.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Dropdown
    },
    setup() {
      const items: DropdownItem[] = [{
        label: 'Profile',
        value: 'profile',
        icon: 'i-carbon-user'
      }, {
        label: 'Settings',
        value: 'settings',
        icon: 'i-carbon-settings'
      }, {
        label: 'Billing',
        value: 'billing',
        icon: 'i-carbon-receipt'
      }, {
        label: 'Sign Out',
        value: 'signout',
        icon: 'i-carbon-logout',
        divider: true
      }];
      const handleSelect = (item: DropdownItem) => {
        alert(\`Selected: \${item.label}\`);
      };
      return {
        items,
        handleSelect
      };
    },
    template: \`
      <div class="p-8 flex justify-end">
        <Dropdown :items="items" placement="bottom-end" @select="handleSelect">
          <template #trigger>
            <button class="flex items-center gap-2 p-2 hover:bg-gray-100 rounded-lg">
              <img
                src="https://i.pravatar.cc/40"
                alt="Avatar"
                class="w-8 h-8 rounded-full"
              />
              <span class="i-carbon-chevron-down"></span>
            </button>
          </template>
        </Dropdown>
      </div>
    \`
  })
}`,...b.parameters?.docs?.source}}};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Dropdown
    },
    setup() {
      const items: DropdownItem[] = [{
        label: 'View Details',
        value: 'view',
        icon: 'i-carbon-view'
      }, {
        label: 'Edit',
        value: 'edit',
        icon: 'i-carbon-edit'
      }, {
        label: 'Duplicate',
        value: 'duplicate',
        icon: 'i-carbon-copy'
      }, {
        label: 'Archive',
        value: 'archive',
        icon: 'i-carbon-archive',
        divider: true
      }, {
        label: 'Delete',
        value: 'delete',
        icon: 'i-carbon-trash-can'
      }];
      const handleSelect = (item: DropdownItem) => {
        alert(\`Action: \${item.label}\`);
      };
      return {
        items,
        handleSelect
      };
    },
    template: \`
      <div class="p-8">
        <table class="w-full">
          <thead>
            <tr class="border-b">
              <th class="text-left p-2">Name</th>
              <th class="text-left p-2">Status</th>
              <th class="text-right p-2">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="i in 3" :key="i" class="border-b">
              <td class="p-2">Item {{ i }}</td>
              <td class="p-2">Active</td>
              <td class="p-2 text-right">
                <Dropdown :items="items" placement="bottom-end" @select="handleSelect">
                  <template #trigger>
                    <button class="p-1 hover:bg-gray-100 rounded">
                      <span class="i-carbon-overflow-menu-vertical"></span>
                    </button>
                  </template>
                </Dropdown>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    \`
  })
}`,...v.parameters?.docs?.source}}};const P=["Default","WithIcons","WithDisabled","RightAligned","UserMenu","TableActions"];export{d as Default,u as RightAligned,v as TableActions,b as UserMenu,m as WithDisabled,p as WithIcons,P as __namedExportsOrder,F as default};
