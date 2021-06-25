<template>
  <div class="editor">
    <h2>{{ team.url ? "Edit" : "New" }} team</h2>

    <label class="input-header">Team name</label>
    <div> <input type='text' v-model="team.name" /></div> 
    <div v-if="isDuplicate" class="warning">
      <svg aria-hidden="true" focusable="false" data-prefix="fas" data-icon="exclamation-triangle" class="svg-inline--fa fa-exclamation-triangle fa-w-18" role="img" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 576 512"><path fill="currentColor" d="M569.517 440.013C587.975 472.007 564.806 512 527.94 512H48.054c-36.937 0-59.999-40.055-41.577-71.987L246.423 23.985c18.467-32.009 64.72-31.951 83.154 0l239.94 416.028zM288 354c-25.405 0-46 20.595-46 46s20.595 46 46 46 46-20.595 46-46-20.595-46-46-46zm-43.673-165.346l7.418 136c.347 6.364 5.609 11.346 11.982 11.346h48.546c6.373 0 11.635-4.982 11.982-11.346l7.418-136c.375-6.874-5.098-12.654-11.982-12.654h-63.383c-6.884 0-12.356 5.78-11.981 12.654z"></path></svg>
      There is already a team with this name
    </div>

    <label class="input-header">Type</label>
    <label><input type="radio" :value="ContextType.Course" v-model="team.contexts[0].type" /> Create a team for the entire course</label>
    <label v-if="course.sections.length > 1"><input type="radio" :value="ContextType.Section" v-model="team.contexts[0].type" @input="team.createSectionChannels = false" /> Create a team for one or more sections</label>
    
    <div v-if="team.contexts[0].type == ContextType.Section">
      <label class="input-header">Sections to link to this team</label>
      <label v-for="sec in sections" :key="sec.name">
        <input type="checkbox" v-model="sec.checked" /> {{ sec.name }}
      </label>
    </div>

    <label class="input-header" v-if="selectedSections.length > 1 || course.groupSets.length > 0">Create private channels ({{ channelCount }}/30) 
      <Tooltip>
        There is a maximum of 30 private channels per team. Please note that channels that have been deleted less than 30 days ago are also counted.
      </Tooltip>
    </label>
    <label v-if="selectedSections.length > 1">
      <input type="checkbox" v-model="team.createSectionChannels" /> For each section 
    </label>
    <label v-for="set in groupSets" :key="set.name">
        <input type="checkbox" v-model="set.checked" />
        For each group in the category {{ set.name }} 
    </label>

    <label class="input-header">Options</label>
    <label><input type="checkbox" v-model="team.allowChannels" /> Allow users to create channels </label>
    <label><input type="checkbox" v-model="team.allowPrivateChannels" /> Allow users to create private channels </label>

    <button @click="save" :disabled="(team.contexts[0].type == ContextType.Section && sections.filter(s => s.checked).length == 0) || team.name.trim().length < 2">{{ team.url ? "Update" : "Create team" }}</button>
    <button @click="$emit('close')" class="button-secondary">Cancel</button>
    <button @click="isDeleting = true" v-if="team.url" class="button-secondary">Delete team</button>
  </div>
  <LoadingScreen v-if="isSaving" />
  <ConfirmDialog v-if="isDeleting" @confirm="deleteTeam" @close="isDeleting = false" />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { ContextType, CourseInfo, Section, Team } from '@/models/CourseInfo';
import LoadingScreen from './LoadingScreen.vue';
import Tooltip from './Tooltip.vue';
import ConfirmDialog from './ConfirmDialog.vue';
import GroupSetWrapper from '@/models/GroupSetWrapper';
import SectionWrapper from '@/models/SectionWrapper';
import axios from 'axios';

@Options({
  props: {
    course: Object,
    team: Object
  },
  components: { LoadingScreen, ConfirmDialog, Tooltip }
})
export default class TeamEditor extends Vue {
  team!: Team;
  course!: CourseInfo;
  ContextType = ContextType;

  sections!: SectionWrapper[];
  groupSets!: GroupSetWrapper[];

  isSaving = false;
  isDeleting = false;

  created(): void {
    this.sections = this.course.sections.map(s => new SectionWrapper(this.team, s));
    this.groupSets = this.course.groupSets.map(s => new GroupSetWrapper(this.team, s));
  }

  save(): void {
    if (this.channelCount > 30) {
      alert('Too many private channels');
      return;
    }
    this.isSaving = true;
    if (this.team.contexts[0].type == ContextType.Section && this.team.contexts[0].id == -1) {
      this.team.contexts = this.team.contexts.slice(1);
    }

    if (!this.team.url) {
      if (this.team.createSectionChannels) {
        for (let sec of this.selectedSections) {
          this.team.channels.push({
            name: sec.name,
            contexts: [{ type: ContextType.Section, id: sec.id }]
          });
        }
      }

      this.course.teams.push(this.team);
      this.team.url = 'test';
    }
    axios.post(process.env.VUE_APP_ENDPOINT + '/CourseInfo', this.team).then(res => {
      this.isSaving = false;
      this.team.id = res.data;
      this.$emit('close');
    });
  }

  deleteTeam(): void {
    this.isDeleting = false;
    this.isSaving = true;
    axios.delete(process.env.VUE_APP_ENDPOINT + '/CourseInfo/' + this.team.id).then(res => {
      this.isSaving = false;
      this.$emit('delete');
    })
  }

  get selectedSections(): Section[] {
    return this.team.contexts[0].type == ContextType.Course ? this.course.sections 
      : this.sections.filter(s => s.checked).map(s => s.section);
  }

  get channelCount(): number {
    return (this.team.createSectionChannels ? this.selectedSections.length : 0) 
      + this.groupSets.filter(g => g.checked).map(g => g.set.groupCount).reduce((a,b) => a+b, 0);
  }

  get isDuplicate(): boolean {
    return this.course.teams.filter(f => f.name == this.team.name && f !== this.team).length > 0;
  }
}
</script>

<style scoped lang="scss">
    .editor {
      font-size: 14px;
    }

    label {
        display: block;

        &.input-header {
          font-size: 16px;
            margin-top: 15px;
            margin-bottom: 3px;
        }
    }

    button {
        margin-top: 20px;
    }

    .warning {
      margin-top: 4px;
      margin-left: 3px;
      color: rgb(112, 97, 10);

      svg {
        width: 15px; 
        position: relative;
        top: 1px;
      }
    }
</style>
