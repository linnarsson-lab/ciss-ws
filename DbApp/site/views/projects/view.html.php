<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewProjects extends JView {

	function display($tpl = null) {
    $projects = $this->get('Projects');
    $this->assignRef('projects', $projects);

    if (count($errors = $this->get('Errors'))) {
      JError::raiseError(500, implode('<br />', $errors));
      return false;
    }
    parent::display($tpl);
	}

}
