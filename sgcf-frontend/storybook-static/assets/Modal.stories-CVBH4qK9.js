import{d as C,w as S,q as D,s as L,k as z,p as E,l as q,T as N,v as T,o as i,c as O,a as x,b as r,x as V,n as _,t as I,r as w,g as o}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as F}from"./_plugin-vue_export-helper-DlAUqK2U.js";const B={class:"nw-modal__header"},U={key:0,class:"nw-modal__title"},j={class:"nw-modal__body"},$={key:0,class:"nw-modal__footer"},k=C({__name:"Modal",props:{modelValue:{type:Boolean},title:{},size:{default:"md"},closeOnOverlay:{type:Boolean,default:!0}},emits:["update:modelValue"],setup(e,{emit:t}){const s=e,h=t,y=()=>h("update:modelValue",!1);S(()=>s.modelValue,l=>{l?document.body.style.overflow="hidden":document.body.style.overflow=""});const M=l=>{l.key==="Escape"&&s.modelValue&&y()};return D(()=>document.addEventListener("keydown",M)),L(()=>{document.removeEventListener("keydown",M),document.body.style.overflow=""}),(l,n)=>(i(),z(T,{to:"body"},[E(N,{name:"modal"},{default:q(()=>[e.modelValue?(i(),O("div",{key:0,class:"nw-modal-overlay",onClick:n[1]||(n[1]=A=>e.closeOnOverlay&&y())},[r("div",{class:_(["nw-modal",`nw-modal--${e.size}`]),onClick:n[0]||(n[0]=V(()=>{},["stop"])),role:"dialog","aria-modal":"true"},[r("div",B,[e.title?(i(),O("h2",U,I(e.title),1)):x("",!0),r("button",{class:"nw-modal__close",onClick:y,"aria-label":"Close"},[...n[2]||(n[2]=[r("span",{class:"i-carbon-close"},null,-1)])])]),r("div",j,[w(l.$slots,"default",{},void 0,!0)]),l.$slots.footer?(i(),O("div",$,[w(l.$slots,"footer",{},void 0,!0)])):x("",!0)],2)])):x("",!0)]),_:3})]))}}),a=F(k,[["__scopeId","data-v-f0037902"]]);k.__docgenInfo={exportName:"default",displayName:"Modal",description:"",tags:{},props:[{name:"modelValue",required:!0,type:{name:"boolean"}},{name:"title",required:!1,type:{name:"string"}},{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'},{name:'"xl"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"closeOnOverlay",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}}],events:[{name:"update:modelValue",type:{names:["boolean"]}}],slots:[{name:"default"},{name:"footer"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/organisms/Modal/Modal.vue"]};const Y={title:"Design System/Organisms/Modal",component:a,tags:["autodocs"],argTypes:{modelValue:{control:"boolean",description:"Estado de abertura do modal (v-model)"},title:{control:"text",description:"Título do modal"},size:{control:"select",options:["sm","md","lg","xl"],description:"Tamanho do modal"},closeOnOverlay:{control:"boolean",description:"Fecha ao clicar no overlay"},closeOnEsc:{control:"boolean",description:"Fecha ao pressionar ESC"},showClose:{control:"boolean",description:"Mostra botão de fechar"}},args:{modelValue:!1,title:"Modal Title",size:"md",closeOnOverlay:!0,closeOnEsc:!0,showClose:!0}},d={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>This is the modal content. You can put any content here.</p>
        </Modal>
      </div>
    `}),args:{title:"Default Modal",size:"md"}},p={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Small Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Small modal with less content.</p>
        </Modal>
      </div>
    `}),args:{title:"Small Modal",size:"sm"}},m={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Large Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Large modal with more space for content.</p>
          <p class="mt-4">Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
        </Modal>
      </div>
    `}),args:{title:"Large Modal",size:"lg"}},c={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Extra Large Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="space-y-4">
            <p>Extra large modal for complex content.</p>
            <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
            <p>Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</p>
          </div>
        </Modal>
      </div>
    `}),args:{title:"Extra Large Modal",size:"xl"}},u={render:e=>({components:{Modal:a},setup(){const t=o(!1);return{isOpen:t,args:e,handleConfirm:()=>{alert("Confirmed!"),t.value=!1}}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal with Footer
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Are you sure you want to proceed with this action?</p>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleConfirm"
                class="px-4 py-2 bg-primary text-white rounded-lg"
              >
                Confirm
              </button>
            </div>
          </template>
        </Modal>
      </div>
    `}),args:{title:"Confirm Action",size:"md"}},g={render:e=>({components:{Modal:a},setup(){const t=o(!1),s=o({name:"",email:""});return{isOpen:t,args:e,formData:s,handleSubmit:()=>{alert(`Submitted: ${JSON.stringify(s.value)}`),t.value=!1}}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Form Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <form @submit.prevent="handleSubmit" class="space-y-4">
            <div>
              <label class="block text-sm font-medium mb-1">Name</label>
              <input
                v-model="formData.name"
                type="text"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg"
                placeholder="Enter your name"
              />
            </div>
            <div>
              <label class="block text-sm font-medium mb-1">Email</label>
              <input
                v-model="formData.email"
                type="email"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg"
                placeholder="Enter your email"
              />
            </div>
          </form>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleSubmit"
                class="px-4 py-2 bg-primary text-white rounded-lg"
              >
                Submit
              </button>
            </div>
          </template>
        </Modal>
      </div>
    `}),args:{title:"User Information",size:"md"}},b={render:e=>({components:{Modal:a},setup(){const t=o(!1);return{isOpen:t,args:e,handleDelete:()=>{alert("Item deleted!"),t.value=!1}}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-red-500 text-white rounded-lg"
        >
          Delete Item
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="text-center">
            <div class="i-carbon-warning text-5xl text-red-500 mx-auto mb-4"></div>
            <p class="text-lg font-semibold mb-2">Delete Item</p>
            <p class="text-gray-600">This action cannot be undone. Are you sure you want to delete this item?</p>
          </div>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleDelete"
                class="px-4 py-2 bg-red-500 text-white rounded-lg"
              >
                Delete
              </button>
            </div>
          </template>
        </Modal>
      </div>
    `}),args:{title:"",size:"sm"}},v={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal with Long Content
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="space-y-4">
            <p v-for="i in 10" :key="i">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.
            </p>
          </div>

          <template #footer>
            <button
              @click="isOpen = false"
              class="px-4 py-2 bg-primary text-white rounded-lg"
            >
              Close
            </button>
          </template>
        </Modal>
      </div>
    `}),args:{title:"Terms and Conditions",size:"lg"}},f={render:e=>({components:{Modal:a},setup(){return{isOpen:o(!1),args:e}},template:`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal (No Close Button)
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>This modal has no close button. Use the Cancel button below to close.</p>

          <template #footer>
            <button
              @click="isOpen = false"
              class="px-4 py-2 bg-gray-200 rounded-lg"
            >
              Cancel
            </button>
          </template>
        </Modal>
      </div>
    `}),args:{title:"Important Notice",size:"md",showClose:!1,closeOnOverlay:!1}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>This is the modal content. You can put any content here.</p>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Default Modal',
    size: 'md'
  }
}`,...d.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Small Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Small modal with less content.</p>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Small Modal',
    size: 'sm'
  }
}`,...p.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Large Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Large modal with more space for content.</p>
          <p class="mt-4">Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Large Modal',
    size: 'lg'
  }
}`,...m.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Extra Large Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="space-y-4">
            <p>Extra large modal for complex content.</p>
            <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
            <p>Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</p>
          </div>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Extra Large Modal',
    size: 'xl'
  }
}`,...c.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      const handleConfirm = () => {
        alert('Confirmed!');
        isOpen.value = false;
      };
      return {
        isOpen,
        args,
        handleConfirm
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal with Footer
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>Are you sure you want to proceed with this action?</p>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleConfirm"
                class="px-4 py-2 bg-primary text-white rounded-lg"
              >
                Confirm
              </button>
            </div>
          </template>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Confirm Action',
    size: 'md'
  }
}`,...u.parameters?.docs?.source}}};g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      const formData = ref({
        name: '',
        email: ''
      });
      const handleSubmit = () => {
        alert(\`Submitted: \${JSON.stringify(formData.value)}\`);
        isOpen.value = false;
      };
      return {
        isOpen,
        args,
        formData,
        handleSubmit
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Form Modal
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <form @submit.prevent="handleSubmit" class="space-y-4">
            <div>
              <label class="block text-sm font-medium mb-1">Name</label>
              <input
                v-model="formData.name"
                type="text"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg"
                placeholder="Enter your name"
              />
            </div>
            <div>
              <label class="block text-sm font-medium mb-1">Email</label>
              <input
                v-model="formData.email"
                type="email"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg"
                placeholder="Enter your email"
              />
            </div>
          </form>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleSubmit"
                class="px-4 py-2 bg-primary text-white rounded-lg"
              >
                Submit
              </button>
            </div>
          </template>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'User Information',
    size: 'md'
  }
}`,...g.parameters?.docs?.source}}};b.parameters={...b.parameters,docs:{...b.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      const handleDelete = () => {
        alert('Item deleted!');
        isOpen.value = false;
      };
      return {
        isOpen,
        args,
        handleDelete
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-red-500 text-white rounded-lg"
        >
          Delete Item
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="text-center">
            <div class="i-carbon-warning text-5xl text-red-500 mx-auto mb-4"></div>
            <p class="text-lg font-semibold mb-2">Delete Item</p>
            <p class="text-gray-600">This action cannot be undone. Are you sure you want to delete this item?</p>
          </div>

          <template #footer>
            <div class="flex gap-2 justify-end">
              <button
                @click="isOpen = false"
                class="px-4 py-2 bg-gray-200 rounded-lg"
              >
                Cancel
              </button>
              <button
                @click="handleDelete"
                class="px-4 py-2 bg-red-500 text-white rounded-lg"
              >
                Delete
              </button>
            </div>
          </template>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: '',
    size: 'sm'
  }
}`,...b.parameters?.docs?.source}}};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal with Long Content
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <div class="space-y-4">
            <p v-for="i in 10" :key="i">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.
            </p>
          </div>

          <template #footer>
            <button
              @click="isOpen = false"
              class="px-4 py-2 bg-primary text-white rounded-lg"
            >
              Close
            </button>
          </template>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Terms and Conditions',
    size: 'lg'
  }
}`,...v.parameters?.docs?.source}}};f.parameters={...f.parameters,docs:{...f.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Modal
    },
    setup() {
      const isOpen = ref(false);
      return {
        isOpen,
        args
      };
    },
    template: \`
      <div>
        <button
          @click="isOpen = true"
          class="px-4 py-2 bg-primary text-white rounded-lg"
        >
          Open Modal (No Close Button)
        </button>

        <Modal v-model="isOpen" v-bind="args">
          <p>This modal has no close button. Use the Cancel button below to close.</p>

          <template #footer>
            <button
              @click="isOpen = false"
              class="px-4 py-2 bg-gray-200 rounded-lg"
            >
              Cancel
            </button>
          </template>
        </Modal>
      </div>
    \`
  }),
  args: {
    title: 'Important Notice',
    size: 'md',
    showClose: false,
    closeOnOverlay: false
  }
}`,...f.parameters?.docs?.source}}};const K=["Default","Small","Large","ExtraLarge","WithFooter","FormModal","DeleteConfirmation","LongContent","NoCloseButton"];export{d as Default,b as DeleteConfirmation,c as ExtraLarge,g as FormModal,m as Large,v as LongContent,f as NoCloseButton,p as Small,u as WithFooter,K as __namedExportsOrder,Y as default};
