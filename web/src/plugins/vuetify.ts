import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import { VBtn } from 'vuetify/components';
import * as directives from 'vuetify/directives';
import { aliases, mdi } from 'vuetify/iconsets/mdi-svg';
import { VDateInput } from 'vuetify/labs/VDateInput';
import 'vuetify/styles';

// BC Gov design token values for Vuetify component overrides.
const FORM_FIELD_BG = '#D8EAFD'; // BC Gov theme-blue-20: light blue tint for VSelect / VDateInput backgrounds
const TERTIARY_BTN_COLOR = '#013366'; // BC Gov primary blue for VBtnTertiary

// https://vuetifyjs.com/en/introduction/why-vuetify/#feature-guides

export default createVuetify({
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: {
      mdi,
    },
  },
  components: {
    ...components,
    VDateInput,
  },
  directives,
  aliases: {
    VBtnSecondary: VBtn,
    VBtnTertiary: VBtn,
  },
  defaults: {
    VContainer: {
      fluid: true,
    },
    VBtn: {
      rounded: true,
      variant: 'flat',
      class: 'text-none',
    },
    VBtnSecondary: {
      rounded: true,
      variant: 'outlined',
      class: 'text-none',
    },
    VBtnTertiary: {
      rounded: true,
      class: 'text-none',
      baseColor: TERTIARY_BTN_COLOR,
    },
    VSelect: {
      bgColor: FORM_FIELD_BG,
      rounded: true,
      variant: 'solo',
      clearable: true,
      density: 'comfortable',
    },
    VTextField: {
      rounded: true,
      dense: true,
      variant: 'outlined',
    },
    VDateInput: {
      density: 'comfortable',
      bgColor: FORM_FIELD_BG,
      variant: 'solo',
      label: 'Date',
      clearable: false,
    },
    VDataTable: {
      hover: true,
      showSelect: true,
      returnObject: true,
      hideDefaultFooter: true,
    },
    VDataTableVirtual: {
      hover: true,
    },
  },
});
