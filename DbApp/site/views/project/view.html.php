<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewProject extends JView {

// Overwriting JView display method
function display($tpl = null) {
    // Assign data to the view
    $project = $this->get('Project');
    $this->assignRef('project', $project);

    $analysis = $this->get('Analysis');
    $this->assignRef('analysis', $analysis);

    $theanalysis = $this->get('TheAnalysis');
    $this->assignRef('theanalysis', $theanalysis);

    $seqbatches = $this->get('SeqBatches');
    $this->assignRef('seqbatches', $seqbatches);

    $afteredit = JRequest::get('POST');
    $this->assignRef('afteredit', $afteredit);

    // Check for errors.
    if (count($errors = $this->get('Errors'))) {
        JError::raiseError(500, implode('<br />', $errors));
        return false;
    }
    // Display the view
    parent::display($tpl);
    }
}
